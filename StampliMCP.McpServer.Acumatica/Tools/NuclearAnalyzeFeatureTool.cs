using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class NuclearAnalyzeFeatureTool
{
    private static readonly Dictionary<string, Guid> _analysisCache = new();

    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> AnalyzeAcumaticaFeature(
        string featureDescription,
        string analysisDepth,
        [Description("Include test scenarios in tasklist")]
        bool includeTestScenarios,
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        // Input validation (Security Enhancement)
        if (string.IsNullOrWhiteSpace(featureDescription))
        {
            return new { error = "Feature description is required" };
        }

        if (featureDescription.Length > 500)
        {
            return new { error = "Feature description exceeds 500 character limit" };
        }

        // Sanitize input - prevent injection
        if (ContainsSuspiciousPatterns(featureDescription))
        {
            return new { error = "Invalid characters in feature description" };
        }

        analysisDepth = string.IsNullOrWhiteSpace(analysisDepth) ? "full" : analysisDepth.ToLower();

        try
        {
            // Generate analysis ID
            var analysisId = $"ana_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Step 1: Identify category from feature description
            var category = await IdentifyCategoryAsync(featureDescription, knowledge, cancellationToken);

            // Step 2: Get operations for category
            var operations = await GetCategoryOperationsAsync(category, knowledge, cancellationToken);

            // Step 3: Analyze each operation
            var operationAnalysis = new List<object>();
            var validations = new List<string>();
            var patterns = new Dictionary<string, string>();

            foreach (var op in operations)
            {
                if (IsOperationRelevant(op, featureDescription))
                {
                    var details = await GetOperationDetailsAsync(op, knowledge, cancellationToken);
                    operationAnalysis.Add(new
                    {
                        name = op["method"],
                        purpose = op["summary"],
                        risk = IdentifyRisks(op)
                    });

                    // Extract validations
                    if (details.ContainsKey("validationRules"))
                    {
                        validations.AddRange(ExtractValidations(details));
                    }

                    // Extract patterns
                    if (details.ContainsKey("implementationPattern"))
                    {
                        ExtractPatterns(details, patterns);
                    }
                }
            }

            // Step 4: Generate tasklist
            var tasklist = GenerateTasklist(
                featureDescription,
                operationAnalysis,
                validations,
                patterns,
                analysisDepth,
                includeTestScenarios
            );

            // Step 5: Build response (Tool Output Schema)
            var response = new Dictionary<string, object>
            {
                ["analysis_id"] = analysisId,
                ["feature"] = featureDescription,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["discovered"] = new Dictionary<string, object>
                {
                    ["primary_category"] = category,
                    ["operations"] = operationAnalysis,
                    ["validations"] = validations,
                    ["patterns"] = patterns
                },
                ["tasklist"] = tasklist,
                ["summary"] = new Dictionary<string, object>
                {
                    ["total_tasks"] = tasklist.Count,
                    ["estimated_complexity"] = EstimateComplexity(tasklist.Count),
                    ["requires_review"] = true,
                    ["ready_for_execution"] = false
                }
            };

            // Cache analysis for execution phase
            _analysisCache[analysisId] = Guid.NewGuid();

            return response;
        }
        catch (Exception ex)
        {
            return new
            {
                error = "Analysis failed",
                details = ex.Message,
                timestamp = DateTime.UtcNow.ToString("O")
            };
        }
    }

    private static bool ContainsSuspiciousPatterns(string input)
    {
        var suspiciousPatterns = new[]
        {
            @"[';""]+\s*(DROP|DELETE|UPDATE|INSERT|EXEC|EXECUTE)",
            @"<script",
            @"javascript:",
            @"\$\{.*\}",
            @"\.\.[\\/]",
            @"cmd\.exe|powershell|bash|sh\s+-c"
        };

        return suspiciousPatterns.Any(pattern =>
            Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
    }

    private static async Task<string> IdentifyCategoryAsync(
        string featureDescription,
        KnowledgeService knowledge,
        CancellationToken ct)
    {
        var categories = await knowledge.GetCategoriesAsync(ct);
        var lowerDesc = featureDescription.ToLower();

        // Category keywords mapping
        var categoryKeywords = new Dictionary<string, string[]>
        {
            ["vendors"] = new[] { "vendor", "supplier", "payee" },
            ["payments"] = new[] { "payment", "pay", "remittance", "check" },
            ["purchaseOrders"] = new[] { "purchase order", "po", "p.o." },
            ["items"] = new[] { "item", "product", "inventory" },
            ["accounts"] = new[] { "account", "gl", "ledger", "chart" },
            ["fields"] = new[] { "field", "custom", "attribute" },
            ["admin"] = new[] { "connect", "config", "setup", "validate" }
        };

        foreach (var kvp in categoryKeywords)
        {
            if (kvp.Value.Any(keyword => lowerDesc.Contains(keyword)))
            {
                return kvp.Key;
            }
        }

        // Default to vendors if no match
        return "vendors";
    }

    private static async Task<List<Dictionary<string, object>>> GetCategoryOperationsAsync(
        string category,
        KnowledgeService knowledge,
        CancellationToken ct)
    {
        var operations = await knowledge.GetOperationsByCategoryAsync(category, ct);

        // Convert Operation objects to Dictionary for flexible processing
        var result = new List<Dictionary<string, object>>();
        foreach (var op in operations)
        {
            var dict = new Dictionary<string, object>
            {
                ["method"] = op.Method,
                ["summary"] = op.Summary,
                ["category"] = op.Category,
                ["enumName"] = op.EnumName
            };

            if (op.RequiredFields != null && op.RequiredFields.Count > 0)
                dict["requiredFields"] = op.RequiredFields;
            if (op.OptionalFields != null)
                dict["optionalFields"] = op.OptionalFields;
            if (op.Pattern != null)
                dict["pattern"] = op.Pattern;
            if (op.ErrorCatalogRef != null)
                dict["errorCatalogRef"] = op.ErrorCatalogRef;

            result.Add(dict);
        }

        return result;
    }

    private static bool IsOperationRelevant(Dictionary<string, object> operation, string featureDescription)
    {
        var lowerDesc = featureDescription.ToLower();
        var method = operation.ContainsKey("method") ? operation["method"]?.ToString()?.ToLower() ?? "" : "";
        var summary = operation.ContainsKey("summary") ? operation["summary"]?.ToString()?.ToLower() ?? "" : "";

        // Check if operation method or summary matches feature
        if (lowerDesc.Contains("export") && method.Contains("export")) return true;
        if (lowerDesc.Contains("import") && method.Contains("get")) return true;
        if (lowerDesc.Contains("create") && method.Contains("export")) return true;
        if (lowerDesc.Contains("update") && method.Contains("export")) return true;
        if (lowerDesc.Contains("duplicate") && (method.Contains("matching") || method.Contains("duplicate"))) return true;
        if (lowerDesc.Contains("validate") && summary.Contains("validat")) return true;

        return false;
    }

    private static Task<Dictionary<string, object>> GetOperationDetailsAsync(
        Dictionary<string, object> operation,
        KnowledgeService knowledge,
        CancellationToken ct)
    {
        // This would normally call the get_operation_details tool
        // For now, return extracted data from operation
        return Task.FromResult(operation);
    }

    private static string IdentifyRisks(Dictionary<string, object> operation)
    {
        var risks = new List<string>();
        var method = operation.ContainsKey("method") ? operation["method"]?.ToString() ?? "" : "";

        if (method.Contains("export") || method.Contains("create"))
            risks.Add("duplicate check required");
        if (method.Contains("payment"))
            risks.Add("financial transaction");
        if (method.Contains("delete") || method.Contains("void"))
            risks.Add("destructive operation");

        return risks.Any() ? string.Join(", ", risks) : "low risk";
    }

    private static List<string> ExtractValidations(Dictionary<string, object> details)
    {
        var validations = new List<string>();

        if (details.ContainsKey("requiredFields"))
        {
            var required = details["requiredFields"];
            if (required is Dictionary<string, FieldInfo> fieldInfoDict)
            {
                foreach (var field in fieldInfoDict)
                {
                    var maxLength = field.Value.MaxLength.HasValue ?
                        $", max {field.Value.MaxLength} chars" : "";
                    validations.Add($"{field.Key}: required{maxLength}");
                }
            }
            else if (required is Dictionary<string, object> objDict)
            {
                foreach (var field in objDict)
                {
                    validations.Add($"{field.Key}: required");
                }
            }
        }

        return validations;
    }

    private static void ExtractPatterns(Dictionary<string, object> details, Dictionary<string, string> patterns)
    {
        patterns["authentication"] = "AcumaticaAuthenticator.authenticatedApiCall";
        patterns["error_handling"] = "response.error field (no exceptions)";
        patterns["json_format"] = "Acumatica nested: {field: {value: data}}";
        patterns["pagination"] = "500 items per page, max 100 pages (delta)";
    }

    private static List<Dictionary<string, object>> GenerateTasklist(
        string featureDescription,
        List<object> operations,
        List<string> validations,
        Dictionary<string, string> patterns,
        string analysisDepth,
        bool includeTestScenarios)
    {
        var tasklist = new List<Dictionary<string, object>>();
        int taskId = 1;

        // Add validation check task
        if (operations.Any())
        {
            tasklist.Add(new Dictionary<string, object>
            {
                ["id"] = taskId++,
                ["type"] = "validation",
                ["priority"] = "high",
                ["action"] = "Check for existing records to prevent duplicates",
                ["operation"] = operations.First(),
                ["auto_approve"] = false
            });
        }

        // Add test tasks if requested
        if (includeTestScenarios && analysisDepth == "full")
        {
            foreach (var validation in validations.Take(3)) // Limit to 3 for brevity
            {
                tasklist.Add(new Dictionary<string, object>
                {
                    ["id"] = taskId++,
                    ["type"] = "test",
                    ["priority"] = "high",
                    ["action"] = $"Write test for: {validation}",
                    ["test_name"] = $"test_{validation.Split(':')[0].Replace(" ", "_")}",
                    ["expected_error"] = ExtractExpectedError(validation),
                    ["auto_approve"] = false
                });
            }
        }

        // Add implementation task
        tasklist.Add(new Dictionary<string, object>
        {
            ["id"] = taskId++,
            ["type"] = "implement",
            ["priority"] = "high",
            ["action"] = $"Implement {featureDescription} with all validations",
            ["location"] = "KotlinAcumaticaDriver.kt",
            ["method"] = ExtractMethodName(featureDescription),
            ["auto_approve"] = false
        });

        // Add verification task
        if (includeTestScenarios)
        {
            tasklist.Add(new Dictionary<string, object>
            {
                ["id"] = taskId++,
                ["type"] = "verify",
                ["priority"] = "high",
                ["action"] = "Run all tests - confirm GREEN phase",
                ["command"] = "gradlew test",
                ["auto_approve"] = false
            });
        }

        return tasklist;
    }

    private static string ExtractExpectedError(string validation)
    {
        if (validation.Contains("required"))
            return $"{validation.Split(':')[0]} is required";
        if (validation.Contains("max"))
        {
            var match = Regex.Match(validation, @"max (\d+) chars");
            if (match.Success)
                return $"{validation.Split(':')[0]} exceeds maximum length of {match.Groups[1].Value} characters";
        }
        return "Validation failed";
    }

    private static string ExtractMethodName(string featureDescription)
    {
        var lower = featureDescription.ToLower();
        if (lower.Contains("export") && lower.Contains("vendor")) return "exportVendor";
        if (lower.Contains("export") && lower.Contains("payment")) return "exportBillPayment";
        if (lower.Contains("export") && lower.Contains("bill")) return "exportAPTransaction";
        if (lower.Contains("import") && lower.Contains("vendor")) return "getVendors";
        return "implementFeature";
    }

    private static string EstimateComplexity(int taskCount)
    {
        if (taskCount <= 3) return "low";
        if (taskCount <= 6) return "medium";
        return "high";
    }

    // Public method to retrieve cached analysis for execute tool
    public static Dictionary<string, object>? GetCachedAnalysis(string analysisId)
    {
        // In production, this would retrieve from a proper cache
        return _analysisCache.ContainsKey(analysisId) ?
            new Dictionary<string, object> { ["id"] = analysisId } :
            null;
    }
}