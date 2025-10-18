using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class KotlinTddWorkflowTool
{

    [McpServerTool(
        Name = "kotlin_tdd_workflow",
        Title = "Kotlin TDD Workflow Generator",
        UseStructuredContent = true
    )]
    [Description(@"
TDD Workflow tool for Kotlin Acumatica implementation using FLOW-BASED architecture.

Returns implementation guide for the matching Acumatica FLOW (9 proven patterns).
Uses flow-based knowledge instead of 48 scattered operations for better focus.

═══════════════════════════════════════════════════════════════════════
YOUR MANDATORY TDD WORKFLOW:
═══════════════════════════════════════════════════════════════════════

STEP 0: GET KOTLIN GOLDEN REFERENCE (MANDATORY FIRST STEP!)
⚠️ CRITICAL: You MUST call the get_kotlin_golden_reference tool BEFORE calling this tool!

The get_kotlin_golden_reference tool returns:
- Full source code of 3 Kotlin files (KotlinAcumaticaDriver.kt, CreateVendorHandler.kt, VendorPayloadMapper.kt)
- Extracted Kotlin patterns: override fun, Handler classes, object singletons, null safety (!!, ?., let, takeIf)
- Error handling rules: NEVER throw exceptions, return error in response.error field
- Infrastructure reuse: ApiCallerFactory, AcumaticaAuthenticator, AcumaticaImportHelper from Java

Why mandatory? exportVendor is the ONLY Kotlin operation implemented - you need its patterns to implement new operations.
Default assumption: All other operations NOT in Kotlin yet (delegate to Java parent).

STEP 1: REVIEW FLOW GUIDANCE
- Tool returns selectedFlow with description and reasoning
- Review relevantOperations that use this flow
- Pick the operation matching user's request

STEP 2: SCAN JAVA LEGACY FILES (MANDATORY - NO EXCEPTIONS!)
- Use Read tool on ALL files in mandatoryFileScanning
- Files use WSL paths (/mnt/c/...)
- Find the keyPatternsToFind listed for each file
- Take notes on: constants, method signatures, patterns
- These are REFERENCE for operation logic - you'll translate to Kotlin using STEP 0 patterns

STEP 3: CREATE TDD TASKLIST
- MUST include '=== FILES SCANNED ===' section proving you read files
- Quote specific constants/methods from BOTH get_kotlin_golden_reference output AND Java legacy files
- 10-20 steps following TDD: RED (tests first) → GREEN (impl using Kotlin patterns) → REFACTOR
- Reference specific line numbers from scanned files
- Use flowAnatomy, codeSnippets, validationRules as guides
- Apply Kotlin patterns from STEP 0 (get_kotlin_golden_reference) to Java logic from STEP 2

FAILURE TO CALL get_kotlin_golden_reference OR SCAN JAVA FILES = REJECTION OF TASKLIST

")]
    public static async Task<object> Execute(
        [Description("Feature description in natural language (e.g., 'vendor import from Acumatica', 'bill payment export')")]
        string feature,

        KnowledgeService knowledge,
        FlowService flowService,
        IntelligenceService intelligence,
        MetricsService metrics,
        JsonFileLogger fileLogger,
        McpResponseLogger responseLogger,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        object? result = null;
        bool success = false;
        string? flowName = null;
        int operationCount = 0;

        // Use Serilog static API (works with static tool methods)
        Serilog.Log.Information("Tool {Tool} started: feature={Feature}",
            "kotlin_tdd_workflow", feature?.Substring(0, Math.Min(50, feature?.Length ?? 0)));

        try
        {
            // Always start new workflow (continue/query/list removed - DEAD CODE)
            var (resultData, selectedFlowName) = await StartWorkflowWithFlow(feature, knowledge, flowService, intelligence, ct);
            flowName = selectedFlowName;
            result = resultData;

            // Extract operation count from result
            if (result != null)
            {
                var resultJson = JsonSerializer.Serialize(result);
                using var doc = JsonDocument.Parse(resultJson);
                if (doc.RootElement.TryGetProperty("relevantOperations", out var ops))
                {
                    operationCount = ops.GetArrayLength();
                }
            }

            success = result is not { } obj || !HasErrorProperty(obj);

            var durationMs = sw.Elapsed.TotalMilliseconds;
            var tokens = result != null ? JsonSerializer.Serialize(result).Length : 0;

            Serilog.Log.Information(
                "Tool {Tool} completed: flow={Flow}, duration={DurationMs}ms, tokens={Tokens}, success={Success}",
                "kotlin_tdd_workflow", flowName, durationMs, tokens, success);

            // Write OpenTelemetry structured log
            try
            {
                await fileLogger.WriteAsync(new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = "Information",
                    Tool = "kotlin_tdd_workflow",
                    Command = "start", // Always start now
                    Flow = flowName,
                    DurationMs = durationMs,
                    Tokens = tokens,
                    Success = success
                });
            }
            catch (Exception logEx)
            {
                Serilog.Log.Warning(logEx, "Failed to write OpenTelemetry log file");
            }

            // Write MCP response log (for test ground truth validation)
            try
            {
                await responseLogger.LogToolExecutionAsync(
                    tool: "kotlin_tdd_workflow",
                    command: "start",
                    context: feature ?? "",
                    flowName: flowName,
                    responseSize: tokens,
                    operationCount: operationCount);
            }
            catch (Exception logEx)
            {
                Serilog.Log.Warning(logEx, "Failed to write MCP response log");
            }

            return result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: feature={Feature}, error={Error}",
                "kotlin_tdd_workflow", feature, ex.Message);

            result = new
            {
                error = "Workflow failed",
                details = ex.Message
            };
            return result;
        }
        finally
        {
            sw.Stop();
            var tokens = result != null ? JsonSerializer.Serialize(result).Length : 0;
            metrics.RecordToolExecution("kotlin_tdd_workflow", "start", sw.Elapsed.TotalMilliseconds, tokens, success, flowName);
        }
    }

    private static bool HasErrorProperty(object obj)
    {
        var type = obj.GetType();
        return type.GetProperty("error") != null;
    }

    private static async Task<(object result, string? flowName)> StartWorkflowWithFlow(
        string feature,
        KnowledgeService knowledge,
        FlowService flowService,
        IntelligenceService intelligence,
        CancellationToken ct)
    {
        string? flowName = null;

        // Validate input
        if (string.IsNullOrWhiteSpace(feature))
        {
            return (new { error = "Feature description is required" }, null);
        }

        // STEP 0: FORCE KOTLIN GOLDEN REFERENCE LOADING
        // Call get_kotlin_golden_reference tool internally to guarantee Kotlin context
        // This bypasses Claude's ability to ignore tool description recommendations
        Serilog.Log.Information("Tool {Tool}: Force-loading Kotlin golden reference internally", "kotlin_tdd_workflow");
        var kotlinGoldenRef = await GetKotlinGoldenReferenceTool.Execute(knowledge, ct);

        // DEBUG: Check if kotlinGoldenRef is populated
        var kotlinRefJson = System.Text.Json.JsonSerializer.Serialize(kotlinGoldenRef);
        Serilog.Log.Information("DEBUG: kotlinGoldenRef object size: {Size} chars JSON", kotlinRefJson.Length);

        // FLOW-BASED TDD ARCHITECTURE:
        // 1. Match feature to flow (9 proven patterns)
        // 2. Return only relevant operations for that flow
        // 3. Include mandatory file scanning with enforcement
        // 4. TDD workflow: RED → GREEN → REFACTOR

        // STEP 1: Match feature to flow
        var (matchedFlowName, confidence, reasoning) = await flowService.MatchFeatureToFlowAsync(feature, ct);
        flowName = matchedFlowName;
        var flowDoc = await flowService.GetFlowAsync(flowName, ct);

        if (flowDoc == null)
        {
            return (new { error = $"Flow '{flowName}' not found. This is a server error." }, flowName);
        }

        var flow = flowDoc.RootElement;

        // STEP 2: Get operations that use this flow
        var usedByOperations = flow.GetProperty("usedByOperations");
        var operations = new List<object>();

        foreach (var opElement in usedByOperations.EnumerateArray())
        {
            var opName = opElement.GetString();
            if (opName == null) continue;

            var op = await knowledge.FindOperationAsync(opName, ct);
            if (op != null)
            {
                operations.Add(new
                {
                    method = op.Method,
                    summary = op.Summary,
                    category = op.Category
                });
            }
        }

        // STEP 3: Load TDD knowledge (keep existing)
        var errorPatterns = await knowledge.GetKotlinErrorPatternsAsync(ct);
        var goldenPatterns = await knowledge.GetKotlinGoldenPatternsAsync(ct);
        var tddWorkflow = await knowledge.GetKotlinTddWorkflowAsync(ct);

        // STEP 4: Build response
        var responseObject = new
        {
            // CONTEXT
            userRequest = feature,

            // KOTLIN GOLDEN REFERENCE (FORCE-LOADED)
            kotlinGoldenReference = new
            {
                note = "⚠️ CRITICAL: This Kotlin context is MANDATORY - read ALL 3 files before scanning Java files!",
                instruction = "SCAN kotlinFiles below to learn Kotlin syntax BEFORE implementing. exportVendor is the ONLY Kotlin operation - use as teaching example.",
                content = kotlinGoldenRef
            },

            // FLOW GUIDANCE
            selectedFlow = new
            {
                name = flowName,
                description = flow.GetProperty("description").GetString(),
                confidence = confidence,
                reasoning = reasoning
            },

            // TDD WORKFLOW
            yourTddWorkflow = @"
OUTPUT EXACTLY THIS FORMAT:

═══ KOTLIN GOLDEN REFERENCE (READ FIRST!) ═══
The kotlinGoldenReference section above contains 3 Kotlin files with FULL SOURCE CODE.
SCAN all 3 files to learn Kotlin patterns before implementing:
- KotlinAcumaticaDriver.kt (override pattern, error handling)
- CreateVendorHandler.kt (null safety !!, ?., let, takeIf)
- VendorPayloadMapper.kt (data object singletons, extension functions)
═══════════════════

═══ JAVA FILES SCANNED (for operation logic) ═══
1. /mnt/c/STAMPLI4/.../[file].java:[lines]
   Constants: [list 2+ from file], Methods: [list 1+ from file], Patterns: [list 1+ from file]
2. [repeat for ALL files in criticalFiles below]
═══════════════════

## TDD Steps
1. [RED] Write test using Kotlin patterns from golden reference...
2. [GREEN] Implement using Kotlin syntax from exportVendor example...

PROOF REQUIRED: Your response must show you read BOTH Kotlin golden reference AND Java files.
",

            // MANDATORY FILE SCANNING
            mandatoryFileScanning = new
            {
                instruction = "You MUST use Read tool on ALL files below before creating tasklist. No exceptions. Proof required.",
                criticalFiles = flow.GetProperty("criticalFiles")
            },

            // OPERATIONS
            relevantOperations = operations,

            // FLOW DETAILS
            flowAnatomy = flow.GetProperty("anatomy"),
            criticalConstants = flow.GetProperty("constants"),
            codeSnippets = flow.TryGetProperty("codeSnippets", out var snippets) ? snippets : (object?)null,
            validationRules = flow.GetProperty("validationRules"),

            // TDD KNOWLEDGE
            tddKnowledge = new
            {
                errorPatterns = errorPatterns,
                goldenPatterns = goldenPatterns,
                workflow = tddWorkflow
            },

            // ENFORCEMENT RULES
            enforcementRules = new
            {
                mustScanFiles = true,
                minimumFilesScanned = flow.GetProperty("criticalFiles").GetArrayLength(),
                requiredProofElements = new[]
                {
                    "File paths with line ranges",
                    "At least 2 constants per file",
                    "At least 1 method signature per file",
                    "At least 1 code pattern per file"
                },
                rejectionCriteria = "Tasklist without '=== FILES SCANNED ===' section will be REJECTED",
                exampleProofFormat = @"
═══ FILES SCANNED (MANDATORY PROOF) ═══
1. /mnt/c/STAMPLI4/.../AcumaticaDriver.java:102-117
   ✓ Constants: RESPONSE_ROWS_LIMIT=2000
   ✓ Methods: getVendors() returns GetVendorsResponse
   ✓ Patterns: new AcumaticaImportHelper<T>(...) { ... }.getValues()

2. /mnt/c/STAMPLI4/.../AcumaticaImportHelper.java:44-200
   ✓ Constants: TIME_LIMIT=10, maxResultsLimit=50000
   ✓ Methods: paginateQuery(client, connectionManager, ...)
   ✓ Patterns: AcumaticaAuthenticator.authenticatedApiCall()
═══════════════════════════════════════"
            },

            // PROJECT PATHS
            projectPaths = new
            {
                moduleRoot = "/mnt/c/STAMPLI4/core/kotlin-erp-harness",
                testFile = "src/test/kotlin/com/stampli/kotlin/driver/KotlinAcumaticaDriverTest.kt",
                implFile = "src/main/kotlin/com/stampli/kotlin/driver/KotlinAcumaticaDriver.kt"
            }
        };

        // STEP 5: Log response to BOTH locations (test isolation + fixed predictable)
        try
        {
            var responseJson = JsonSerializer.Serialize(responseObject);
            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                tool = "kotlin_tdd_workflow",
                command = "start",
                context = feature,
                flowName = flowName,
                responseSize = responseJson.Length,
                operationCount = operations.Count
            };
            var logJson = JsonSerializer.Serialize(logEntry);

            // PRIMARY: Test isolation directory (MCP_LOG_DIR for test runs)
            var testLogDir = Environment.GetEnvironmentVariable("MCP_LOG_DIR");
            Console.Error.WriteLine($"[MCP] MCP_LOG_DIR environment variable: {testLogDir ?? "(not set)"}");

            string? testLogPath = null;
            if (!string.IsNullOrEmpty(testLogDir))
            {
                try
                {
                    Console.Error.WriteLine($"[MCP] Creating test log directory: {testLogDir}");
                    if (!Directory.Exists(testLogDir))
                    {
                        Directory.CreateDirectory(testLogDir);
                        Console.Error.WriteLine($"[MCP] Test log directory created");
                    }
                    else
                    {
                        Console.Error.WriteLine($"[MCP] Test log directory already exists");
                    }

                    testLogPath = Path.Combine(testLogDir, $"mcp_flow_{DateTime.Now:yyyyMMdd_HHmmss}.jsonl");
                    await File.AppendAllTextAsync(testLogPath, logJson + "\n", ct);
                    Console.Error.WriteLine($"[MCP] Test log written successfully: {testLogPath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[MCP] Test log write FAILED: {ex.Message}");
                    Console.Error.WriteLine($"[MCP] Exception type: {ex.GetType().Name}");
                }
            }
            else
            {
                Console.Error.WriteLine($"[MCP] Skipping test-isolated logging (MCP_LOG_DIR not set)");
            }

            // SECONDARY: Fixed predictable location (ALWAYS available for verification)
            var fixedLogDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
            try
            {
                if (!Directory.Exists(fixedLogDir))
                {
                    Directory.CreateDirectory(fixedLogDir);
                }
                var fixedLogPath = Path.Combine(fixedLogDir, $"mcp_responses_{DateTime.Now:yyyyMMdd}.jsonl");
                await File.AppendAllTextAsync(fixedLogPath, logJson + "\n", ct);

                // Write FULL response content for format verification
                var fullResponsePath = Path.Combine(fixedLogDir, $"mcp_full_response_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                await File.WriteAllTextAsync(fullResponsePath, responseJson, ct);

                Console.Error.WriteLine($"[MCP] kotlin_tdd_workflow: flow={flowName}, ops={operations.Count}, size={responseJson.Length} chars");
                Console.Error.WriteLine($"[MCP] Logs → TEST: {testLogPath ?? "none"} | FIXED: {fixedLogPath}");
                Console.Error.WriteLine($"[MCP] Full response: {fullResponsePath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] Fixed log write failed: {ex.Message}");
            }
        }
        catch (Exception logEx)
        {
            Console.Error.WriteLine($"[MCP] Logging failed: {logEx.Message}");
        }

        // DEBUG: Check final response size before returning
        var finalResponseJson = System.Text.Json.JsonSerializer.Serialize(responseObject);
        Serilog.Log.Information("DEBUG: Final responseObject size: {Size} chars JSON (includes kotlinGoldenReference section)",
            finalResponseJson.Length);

        return (responseObject, flowName);
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

        // Score each category by counting matching keywords
        var categoryScores = new Dictionary<string, int>();
        foreach (var kvp in categoryKeywords)
        {
            var score = CountKeywordMatches(lowerDesc, kvp.Value);
            if (score > 0)
            {
                categoryScores[kvp.Key] = score;
            }
        }

        // Return category with highest score (most keyword matches)
        if (categoryScores.Any())
        {
            var bestMatch = categoryScores.OrderByDescending(kvp => kvp.Value).First();
            return bestMatch.Key;
        }

        return "unknown";
    }

    private static int CountKeywordMatches(string text, string[] keywords)
    {
        return keywords.Count(keyword => text.Contains(keyword));
    }

    private static string ConvertToWSLPath(string windowsPath)
    {
        // Convert Windows path to WSL format for Claude CLI running in bash context
        // C:\STAMPLI4\core\... → /mnt/c/STAMPLI4/core/...
        if (string.IsNullOrEmpty(windowsPath)) return windowsPath;

        return windowsPath
            .Replace(@"C:\", "/mnt/c/")
            .Replace(@"c:\", "/mnt/c/")
            .Replace("\\", "/");
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