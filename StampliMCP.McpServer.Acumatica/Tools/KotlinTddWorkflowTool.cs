using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class KotlinTddWorkflowTool
{
    private static readonly Dictionary<string, WorkflowSession> _sessions = new();
    private static readonly object _sessionLock = new();
    private const int MaxSessions = 100;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    private class WorkflowSession
    {
        public string Id { get; set; } = string.Empty;
        public string Phase { get; set; } = "DISCOVER";
        public Operation? Operation { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<string> CompletedTasks { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    }

    private static void CleanupExpiredSessions()
    {
        lock (_sessionLock)
        {
            var now = DateTime.UtcNow;
            var expiredSessions = _sessions
                .Where(kvp => now - kvp.Value.LastAccessedAt > SessionTimeout)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.Remove(sessionId);
            }
        }
    }

    private static WorkflowSession? GetSession(string sessionId)
    {
        lock (_sessionLock)
        {
            CleanupExpiredSessions();

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session;
            }
            return null;
        }
    }

    private static void SaveSession(WorkflowSession session)
    {
        lock (_sessionLock)
        {
            CleanupExpiredSessions();

            // Enforce max sessions limit
            if (_sessions.Count >= MaxSessions && !_sessions.ContainsKey(session.Id))
            {
                // Remove oldest session
                var oldestSession = _sessions.OrderBy(kvp => kvp.Value.LastAccessedAt).First();
                _sessions.Remove(oldestSession.Key);
            }

            _sessions[session.Id] = session;
        }
    }

    private static void RemoveSession(string sessionId)
    {
        lock (_sessionLock)
        {
            _sessions.Remove(sessionId);
        }
    }

    [McpServerTool(Name = "kotlin_tdd_workflow")]
    [Description("Single entry point for Kotlin TDD implementation. Handles all phases: discovery, test writing, implementation, validation. Commands: 'start' (new feature), 'continue' (next phase), 'query' (get help), 'list' (show operations).")]
    public static async Task<object> Execute(
        [Description("Command: 'start' (new feature), 'continue' (next phase), 'query' (get help), 'list' (show operations)")]
        string command,

        [Description("Context: feature description for 'start', progress report for 'continue', question for 'query'")]
        string context,

        [Description("Session ID from previous response (null for 'start')")]
        string? sessionId,

        KnowledgeService knowledge,
        SearchService search,
        IntelligenceService intelligence,
        CancellationToken ct)
    {
        try
        {
            return command?.ToLower() switch
            {
                "start" => await StartWorkflow(context, knowledge, intelligence, ct),
                "continue" => await ContinueWorkflow(sessionId ?? "", context, knowledge, ct),
                "query" => await QueryHelp(context, sessionId, knowledge, search, ct),
                "list" => await ListAvailableOperations(knowledge, ct),
                _ => new { error = "Unknown command. Use: start, continue, query, or list" }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                error = "Workflow failed",
                details = ex.Message,
                hint = "Try 'query' command for help or 'list' to see available operations"
            };
        }
    }

    private static async Task<object> StartWorkflow(string feature, KnowledgeService knowledge, IntelligenceService intelligence, CancellationToken ct)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(feature))
        {
            return new { error = "Feature description is required" };
        }

        // 1. Identify category using the existing logic from NuclearAnalyzeFeatureTool
        var category = await IdentifyCategory(feature, knowledge, ct);

        // Handle unknown category
        if (category == "unknown")
        {
            var categories = await knowledge.GetCategoriesAsync(ct);
            return new
            {
                needsClarification = true,
                message = "Can't identify operation category. Please be more specific or choose from available categories.",
                availableCategories = categories.Select(c => new
                {
                    name = c.Name,
                    description = c.Description,
                    operationCount = c.Count
                }),
                examples = new[]
                {
                    "export vendor to Acumatica",
                    "create bill payment",
                    "retrieve purchase orders",
                    "validate GL accounts"
                }
            };
        }

        // 2. Find best matching operation
        var operations = await knowledge.GetOperationsByCategoryAsync(category, ct);
        var operation = FindBestMatch(feature, operations);

        if (operation == null)
        {
            return new
            {
                error = "No matching operation found",
                category = category,
                availableOperations = operations.Select(o => new
                {
                    method = o.Method,
                    summary = o.Summary
                }),
                suggestion = $"Try one of these: {string.Join(", ", operations.Take(3).Select(o => o.Method))}"
            };
        }

        // 3. Load ALL knowledge upfront
        var validationRules = GetValidationRules(operation);
        var errorMessages = await GetErrorMessages(operation.Method, knowledge, ct);
        var testTemplate = GenerateTestTemplate(operation);
        var implTemplate = GenerateImplementationTemplate(operation);

        // 4. Create session
        var session = new WorkflowSession
        {
            Id = $"tdd_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}",
            Phase = "RED",
            Operation = operation,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };
        SaveSession(session);

        // 5. Return EVERYTHING the LLM needs
        return new
        {
            sessionId = session.Id,
            phase = "RED",
            operation = new
            {
                method = operation.Method,
                category = category,
                summary = operation.Summary,
                enumName = operation.EnumName
            },

            // Complete tasklist
            tasklist = new[]
            {
                $"1. [RED] Write test for {operation.Method} in KotlinAcumaticaDriverTest.kt - MUST FAIL first",
                $"2. [RED] Add validation tests for: {string.Join(", ", validationRules.Select(r => r["field"]))}",
                "3. [RED] Run 'mvn test' and confirm all new tests fail",
                $"4. [GREEN] Implement {operation.Method} in KotlinAcumaticaDriver.kt",
                "5. [GREEN] Add field validations with exact error messages from templates",
                "6. [GREEN] Run 'mvn test' until all tests pass",
                "7. [REFACTOR] Optional: Extract validation to separate Validator class",
                "8. [COMPLETE] Commit with message describing the feature"
            },

            // All knowledge provided upfront
            knowledge = new
            {
                testCode = testTemplate,
                implementationCode = implTemplate,

                validationRules = validationRules,
                errorMessages = errorMessages,

                requiredFields = operation.RequiredFields?.Select(f => new
                {
                    name = f.Key,
                    type = f.Value.Type,
                    maxLength = f.Value.MaxLength,
                    description = f.Value.Description
                }),

                optionalFields = operation.OptionalFields,

                apiEndpoint = operation.ApiEndpoint != null ? new
                {
                    entity = operation.ApiEndpoint.Entity,
                    method = operation.ApiEndpoint.Method,
                    urlPattern = operation.ApiEndpoint.UrlPattern
                } : null,

                files = new
                {
                    moduleRoot = @"C:\STAMPLI4\core\kotlin-erp-harness",
                    testFile = @"src\test\kotlin\com\stampli\kotlin\driver\KotlinAcumaticaDriverTest.kt",
                    implFile = @"src\main\kotlin\com\stampli\kotlin\driver\KotlinAcumaticaDriver.kt"
                },

                legacyFiles = operation.ScanThese?.Select(s => new
                {
                    path = $@"C:\STAMPLI4\core\{s.File}",
                    lines = s.Lines,
                    purpose = s.Purpose,
                    hint = $"Read lines {s.Lines} for {s.Purpose}"
                })
            },

            nextAction = "Write the test code first in KotlinAcumaticaDriverTest.kt. The test MUST fail before implementing.",

            hints = new[]
            {
                "Use the testCode template exactly as provided",
                "Tests must fail first (RED phase is mandatory)",
                "Use exact error messages from errorMessages field",
                "Test data should use System.currentTimeMillis() for uniqueness",
                "Connection properties: hostname=http://63.32.187.185/StampliAcumaticaDB, user=admin, password=Password1"
            }
        };
    }

    private static async Task<object> ContinueWorkflow(string sessionId, string progressReport, KnowledgeService knowledge, CancellationToken ct)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            return new
            {
                error = "Session not found or expired",
                suggestion = "Start a new workflow with 'start' command",
                hint = $"Sessions expire after {SessionTimeout.TotalMinutes} minutes of inactivity"
            };
        }

        var reportLower = progressReport.ToLower();

        switch (session.Phase)
        {
            case "RED":
                if (reportLower.Contains("fail") || reportLower.Contains("red") || reportLower.Contains("failing"))
                {
                    session.Phase = "GREEN";
                    session.CompletedTasks.Add("Tests written and failing");

                    return new
                    {
                        sessionId = session.Id,
                        phase = "GREEN",
                        message = "Excellent! Tests are failing as expected (RED phase complete).",
                        completedTasks = session.CompletedTasks,
                        nextAction = "Now implement the Kotlin code in KotlinAcumaticaDriver.kt to make tests pass",
                        implementationGuidance = new
                        {
                            useAuthWrapper = "Always use AcumaticaAuthenticator.authenticatedApiCall",
                            apiCallerFactory = "Create with ApiCallerFactory()",
                            jsonFormat = "Acumatica uses nested value objects: {\"VendorName\": {\"value\": \"Name\"}}",
                            errorHandling = "Set response.error and return immediately, never throw exceptions"
                        },
                        reminder = "Implement minimal code to make tests pass. Don't over-engineer in GREEN phase."
                    };
                }
                else
                {
                    return new
                    {
                        error = "Tests must fail first!",
                        phase = "RED",
                        hint = "Ensure tests are actually failing before implementing",
                        checklist = new[]
                        {
                            "Did you run 'mvn test'?",
                            "Are the new tests actually being executed?",
                            "Check test output for failures"
                        }
                    };
                }

            case "GREEN":
                if (reportLower.Contains("pass") || reportLower.Contains("green") || reportLower.Contains("passing"))
                {
                    session.Phase = "REFACTOR";
                    session.CompletedTasks.Add("Implementation complete, tests passing");

                    return new
                    {
                        sessionId = session.Id,
                        phase = "REFACTOR",
                        message = "Great! All tests are passing (GREEN phase complete).",
                        completedTasks = session.CompletedTasks,
                        refactoringOptions = new[]
                        {
                            "Extract validation logic to a separate Validator class",
                            "Create a PayloadMapper for JSON transformation",
                            "Add logging with appropriate log levels",
                            "Extract constants for magic numbers/strings",
                            "Consider adding integration test with real Acumatica instance"
                        },
                        nextAction = "Optional: Refactor while keeping tests green, or say 'continue' to complete"
                    };
                }
                else
                {
                    return new
                    {
                        phase = "GREEN",
                        hint = "Keep working until all tests pass",
                        debugTips = new[]
                        {
                            "Check exact error message text (case-sensitive)",
                            "Verify field names match exactly (vendorName vs VendorName)",
                            "Ensure authentication is configured correctly",
                            "Check JSON format - Acumatica needs nested value objects",
                            "Verify API endpoint URL construction"
                        },
                        commonIssues = new[]
                        {
                            "Wrong error message: Must match EXACTLY from templates",
                            "Missing authentication: Wrap in AcumaticaAuthenticator.authenticatedApiCall",
                            "JSON format: Use {\"FieldName\": {\"value\": \"data\"}} not just {\"FieldName\": \"data\"}"
                        }
                    };
                }

            case "REFACTOR":
                session.Phase = "COMPLETE";
                session.CompletedTasks.Add("Refactoring complete (optional)");

                var summary = new
                {
                    sessionId = session.Id,
                    phase = "COMPLETE",
                    message = $"Successfully implemented {session.Operation?.Method} using TDD workflow!",
                    summary = new
                    {
                        operation = session.Operation?.Method,
                        category = session.Category,
                        tddPhases = new[] { "RED (tests failed)", "GREEN (tests pass)", "REFACTOR (optional)" },
                        completedTasks = session.CompletedTasks,
                        filesModified = new[]
                        {
                            "KotlinAcumaticaDriverTest.kt",
                            "KotlinAcumaticaDriver.kt"
                        }
                    },
                    nextSteps = new[]
                    {
                        "Run 'mvn clean install' to ensure full build passes",
                        "Commit your changes with descriptive message",
                        "Consider adding integration tests",
                        "Document any special considerations",
                        "Start new feature with 'start' command"
                    }
                };

                // Clean up session
                RemoveSession(sessionId);

                return summary;

            default:
                return new { error = "Unknown phase", currentPhase = session.Phase };
        }
    }

    private static async Task<object> QueryHelp(string question, string? sessionId, KnowledgeService knowledge, SearchService search, CancellationToken ct)
    {
        var questionLower = question.ToLower();

        // Authentication issues
        if (questionLower.Contains("auth") || questionLower.Contains("401") || questionLower.Contains("unauthorized"))
        {
            return new
            {
                problem = "Authentication issue",
                solution = "Always use AcumaticaAuthenticator.authenticatedApiCall wrapper",
                kotlinExample = @"
val apiCaller = apiCallerFactory.createPutRestApiCaller(
    request,
    AcumaticaEndpoint.VENDOR,
    AcumaticaUrlSuffixAssembler(),
    jsonPayload
)

val apiResponse = AcumaticaAuthenticator.authenticatedApiCall(
    request,
    apiCallerFactory
) { apiCaller.call() }",
                checkList = new[]
                {
                    "Verify connectionProperties contains: hostname, user, password",
                    "Check URL format: http://63.32.187.185/StampliAcumaticaDB",
                    "No trailing slash in hostname",
                    "Ensure subsidiary is set to 'StampliCompany'"
                }
            };
        }

        // Field validation questions
        if (questionLower.Contains("field") || questionLower.Contains("required") || questionLower.Contains("validation"))
        {
            if (!string.IsNullOrEmpty(sessionId))
            {
                var session = GetSession(sessionId);
                if (session?.Operation != null)
                {
                    return new
                    {
                        operation = session.Operation.Method,
                        requiredFields = session.Operation.RequiredFields?.Select(f => new
                        {
                            field = f.Key,
                            type = f.Value.Type,
                            maxLength = f.Value.MaxLength,
                            errorIfMissing = $"{f.Key} is required",
                            errorIfTooLong = f.Value.MaxLength > 0 ? $"{f.Key} exceeds maximum length of {f.Value.MaxLength} characters" : null
                        }),
                        validationExample = @"
val vendorName = rawData[""vendorName""]
if (vendorName.isNullOrBlank()) {
    response.error = ""vendorName is required""
    return response
}
if (vendorName.length > 60) {
    response.error = ""vendorName exceeds maximum length of 60 characters""
    return response
}"
                    };
                }
            }

            return new
            {
                hint = "Start a workflow first to get operation-specific field requirements",
                generalValidationRules = new[]
                {
                    "All error messages must match EXACTLY (case-sensitive)",
                    "Check required fields before optional ones",
                    "Return immediately on first validation error",
                    "Never throw exceptions, always set response.error"
                }
            };
        }

        // TDD workflow questions
        if (questionLower.Contains("tdd") || questionLower.Contains("workflow") || questionLower.Contains("phase"))
        {
            var currentPhase = "Not in session";
            if (!string.IsNullOrEmpty(sessionId))
            {
                var session = GetSession(sessionId);
                if (session != null)
                {
                    currentPhase = session.Phase;
                }
            }

            return new
            {
                tddWorkflow = new
                {
                    phases = new[]
                    {
                        "RED - Write tests that fail",
                        "GREEN - Write minimal code to pass tests",
                        "REFACTOR - Improve code while keeping tests green"
                    },
                    currentPhase = currentPhase,
                    rules = new[]
                    {
                        "NEVER skip RED phase - tests must fail first",
                        "Write minimal code in GREEN phase",
                        "Refactoring is optional but recommended",
                        "All tests must pass before moving to next operation"
                    }
                }
            };
        }

        // Search for operations
        if (questionLower.Contains("find") || questionLower.Contains("search"))
        {
            var searchTerm = question.Split(' ').LastOrDefault() ?? "vendor";
            var results = await search.SearchAsync(searchTerm, ct);
            return new
            {
                searchTerm = searchTerm,
                matches = results.Take(5).Select(r => new
                {
                    operation = r.Operation,
                    match = r.Match
                }),
                hint = "Use 'start [operation name]' to begin implementation"
            };
        }

        // Default help
        return new
        {
            availableCommands = new[]
            {
                "start [feature description] - Begin new TDD implementation",
                "continue [progress report] - Move to next TDD phase",
                "query [question] - Get specific help",
                "list - Show all available operations"
            },
            examples = new[]
            {
                "start export vendor to acumatica",
                "continue tests are failing",
                "query how to handle authentication",
                "list"
            },
            commonIssues = new[]
            {
                "Authentication: Always wrap in AcumaticaAuthenticator",
                "Validation: Use exact error messages from templates",
                "JSON Format: Acumatica needs nested value objects",
                "Testing: Tests MUST fail first (RED phase)"
            },
            resources = new
            {
                testInstance = "http://63.32.187.185/StampliAcumaticaDB",
                credentials = "admin / Password1",
                moduleLocation = @"C:\STAMPLI4\core\kotlin-erp-harness",
                mavenCommand = "mvn test"
            }
        };
    }

    private static async Task<object> ListAvailableOperations(KnowledgeService knowledge, CancellationToken ct)
    {
        var categories = await knowledge.GetCategoriesAsync(ct);
        var allOperations = new List<object>();

        foreach (var category in categories)
        {
            var operations = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            allOperations.Add(new
            {
                category = category.Name,
                description = category.Description,
                operations = operations.Select(o => new
                {
                    method = o.Method,
                    summary = o.Summary
                })
            });
        }

        return new
        {
            totalCategories = categories.Count,
            totalOperations = categories.Sum(c => c.Count),
            categories = allOperations,
            usage = "Use 'start [operation or feature description]' to begin implementation",
            examples = new[]
            {
                "start exportVendor",
                "start create bill payment",
                "start retrieve purchase orders"
            }
        };
    }

    // Helper methods
    private static async Task<string> IdentifyCategory(string featureDescription, KnowledgeService knowledge, CancellationToken ct)
    {
        var categories = await knowledge.GetCategoriesAsync(ct);
        var lowerDesc = featureDescription.ToLower();

        var categoryKeywords = new Dictionary<string, string[]>
        {
            ["vendors"] = new[] { "vendor", "supplier", "payee" },
            ["payments"] = new[] { "payment", "pay", "remittance", "check", "bill payment" },
            ["purchaseOrders"] = new[] { "purchase order", "po", "p.o.", "purchase", "order" },
            ["items"] = new[] { "item", "product", "inventory", "material" },
            ["accounts"] = new[] { "account", "gl", "ledger", "chart", "general ledger" },
            ["fields"] = new[] { "field", "custom", "attribute", "property" },
            ["admin"] = new[] { "connect", "config", "setup", "validate", "validation" },
            ["retrieval"] = new[] { "duplicate", "check", "retrieve", "fetch" },
            ["utility"] = new[] { "debug", "test", "utility" }
        };

        foreach (var kvp in categoryKeywords)
        {
            if (kvp.Value.Any(keyword => lowerDesc.Contains(keyword)))
            {
                return kvp.Key;
            }
        }

        return "unknown";
    }

    private static Operation? FindBestMatch(string feature, List<Operation> operations)
    {
        var lowerFeature = feature.ToLower();

        // Exact method name match
        var exactMatch = operations.FirstOrDefault(o =>
            o.Method.Equals(feature, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null) return exactMatch;

        // Method name contains
        var methodMatch = operations.FirstOrDefault(o =>
            o.Method.ToLower().Contains(lowerFeature) ||
            lowerFeature.Contains(o.Method.ToLower()));
        if (methodMatch != null) return methodMatch;

        // Summary contains feature keywords
        var summaryMatch = operations.FirstOrDefault(o =>
            o.Summary?.ToLower().Contains(lowerFeature) == true);
        if (summaryMatch != null) return summaryMatch;

        // Partial matches on key terms
        var keywords = lowerFeature.Split(' ').Where(w => w.Length > 3).ToList();
        if (keywords.Any())
        {
            var keywordMatch = operations.FirstOrDefault(o =>
                keywords.Any(k => o.Method.ToLower().Contains(k) ||
                                  o.Summary?.ToLower().Contains(k) == true));
            if (keywordMatch != null) return keywordMatch;
        }

        // Default to first operation in category
        return operations.FirstOrDefault();
    }

    private static List<Dictionary<string, object>> GetValidationRules(Operation operation)
    {
        var rules = new List<Dictionary<string, object>>();

        if (operation.RequiredFields != null)
        {
            foreach (var field in operation.RequiredFields)
            {
                var rule = new Dictionary<string, object>
                {
                    ["field"] = field.Key,
                    ["required"] = true,
                    ["type"] = field.Value.Type
                };

                if (field.Value.MaxLength > 0)
                {
                    rule["maxLength"] = field.Value.MaxLength;
                    rule["lengthError"] = $"{field.Key} exceeds maximum length of {field.Value.MaxLength} characters";
                }

                rule["requiredError"] = $"{field.Key} is required";
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static async Task<List<string>> GetErrorMessages(string operation, KnowledgeService knowledge, CancellationToken ct)
    {
        var errors = new List<string>();
        var catalog = await knowledge.GetErrorCatalogAsync(ct);

        // Add operation-specific errors if available
        if (catalog.OperationErrors?.ContainsKey(operation) == true)
        {
            var operationErrors = catalog.OperationErrors[operation];
            if (operationErrors.Validation != null)
            {
                errors.AddRange(operationErrors.Validation.Select(e => e.Message));
            }
            if (operationErrors.BusinessLogic != null)
            {
                errors.AddRange(operationErrors.BusinessLogic.Select(e => e.Message));
            }
        }

        // Add common validation errors
        errors.Add("Missing required field");
        errors.Add("Field exceeds maximum length");
        errors.Add("Invalid field value");

        return errors;
    }

    private static string GenerateTestTemplate(Operation operation)
    {
        return $@"
@Test
fun `{operation.Method} creates successfully`() {{
    // Arrange
    val request = {GetRequestClassName(operation.Method)}().apply {{
        subsidiary = ""StampliCompany""
        dualDriverName = ""com.stampli.kotlin.driver.KotlinAcumaticaDriver""
        connectionProperties = connectionProperties
        finSysBridgeTransferredObject = FinSysBridgeTransferredObject()

        rawData = mapOf(
            // Add required fields here based on operation
            ""stampliLink"" to ""https://app.stampli.com/test_${{System.currentTimeMillis()}}""
        )
    }}

    // Act
    val response = driver.{operation.Method}(request)

    // Assert
    assertNull(response.error, ""Unexpected error: ${{response.error}}"")
    assertNotNull(response.response)
}}

@Test
fun `{operation.Method} validates required fields`() {{
    // Test each required field validation
    val request = createRequest(/* missing required field */)
    val response = driver.{operation.Method}(request)

    assertNotNull(response.error)
    assertEquals(""[field] is required"", response.error)
}}";
    }

    private static string GenerateImplementationTemplate(Operation operation)
    {
        return $@"
override fun {operation.Method}(request: {GetRequestClassName(operation.Method)}): {GetResponseClassName(operation.Method)} {{
    val response = {GetResponseClassName(operation.Method)}()

    // Get raw data
    val rawData = request.rawData
    if (rawData == null) {{
        response.error = ""Missing data""
        return response
    }}

    // Validation - add for each required field
    val requiredField = rawData[""fieldName""]
    if (requiredField.isNullOrBlank()) {{
        response.error = ""fieldName is required""
        return response
    }}

    // Build JSON payload
    val jsonPayload = buildJsonObject {{
        // Acumatica format: nested value objects
        put(""FieldName"", buildJsonObject {{
            put(""value"", requiredField)
        }})
    }}

    // Create API caller
    val apiCaller = apiCallerFactory.createPutRestApiCaller(
        request,
        AcumaticaEndpoint.{GetEndpointName(operation.Method)},
        AcumaticaUrlSuffixAssembler(),
        jsonPayload.toString()
    )

    // Make authenticated API call
    val apiResponse = AcumaticaAuthenticator.authenticatedApiCall(
        request,
        apiCallerFactory
    ) {{ apiCaller.call() }}

    // Handle response
    if (!apiResponse.isSuccessful) {{
        response.error = ""API call failed with code ${{apiResponse.responseCode}}""
        return response
    }}

    // Parse response and extract ID
    response.response = CsvLinkBridgeObject().apply {{
        // Extract ID from apiResponse.content
    }}

    return response
}}";
    }

    private static string GetRequestClassName(string method)
    {
        // Map operation method to request class name
        return method switch
        {
            "exportVendor" => "ExportVendorRequest",
            "getVendors" => "GetVendorsRequest",
            "exportAPTransaction" => "ExportAPTransactionRequest",
            "exportBillPayment" => "ExportBillPaymentRequest",
            _ => "Request"
        };
    }

    private static string GetResponseClassName(string method)
    {
        // Map operation method to response class name
        return method switch
        {
            var m when m.StartsWith("export") => "ExportResponse",
            var m when m.StartsWith("get") => $"{m}Response",
            var m when m.StartsWith("retrieve") => $"{m}Response",
            _ => "Response"
        };
    }

    private static string GetEndpointName(string method)
    {
        // Map operation method to Acumatica endpoint
        return method switch
        {
            var m when m.Contains("Vendor") => "VENDOR",
            var m when m.Contains("Payment") => "PAYMENT",
            var m when m.Contains("Bill") => "BILL",
            var m when m.Contains("PurchaseOrder") => "PURCHASE_ORDER",
            _ => "DEFAULT"
        };
    }
}