using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ErrorDiagnosticTool
{
    public sealed class ErrorContext
    {
        [Description("Operation where error occurred")]
        public string? Operation { get; set; }

        [Description("Request payload that caused error (optional snippet)")]
        public string? PayloadSnippet { get; set; }
    }

    [McpServerTool(
        Name = "diagnose_error",
        Title = "Error Diagnostic",
        UseStructuredContent = true
    )]
    [Description(@"
Intelligent error diagnostics with context gathering via elicitation.

Analyzes Acumatica errors and provides:
✓ Error category (Validation, BusinessLogic, Authentication, Network, Unknown)
✓ Possible causes
✓ Solutions with code examples
✓ Related flow validation rules
✓ Prevention tips

Uses elicitation to gather context when error message alone isn't sufficient.

Categories:
• Validation - Field format, length, required fields
• BusinessLogic - Acumatica-specific rules (duplicate vendors, etc.)
• Authentication - Session/credentials issues
• Network - Timeouts, connection errors
• Unknown - Unrecognized errors
")]
    public static async Task<CallToolResult> Execute(
        [Description("Error message from Acumatica API")]
        string errorMessage,

        ModelContextProtocol.Server.McpServer server,
        FlowService flowService,
        KnowledgeService knowledge,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: error={Error}",
            "diagnose_error", errorMessage.Substring(0, Math.Min(50, errorMessage.Length)));

        try
        {
            // Step 1: Categorize error
            var category = CategorizeError(errorMessage);

            // Step 2: Elicit context if error is ambiguous
            string? operation = null;
            string? payload = null;

            if (category == "Unknown" || errorMessage.Length < 20)
            {
                try
                {
                    var schema = new ElicitRequestParams.RequestSchema
                    {
                        Properties =
                        {
                            ["operation"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Operation where error occurred (e.g., exportVendor)"
                            },
                            ["payloadSnippet"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Optional request payload snippet"
                            }
                        }
                    };

                    var elicitResult = await server.ElicitAsync(new ElicitRequestParams
                    {
                        Message = "Need more context to diagnose. Provide operation and/or payload snippet:",
                        RequestedSchema = schema
                    }, ct);

                    if (elicitResult.Action == "accept" && elicitResult.Content is { } content)
                    {
                        if (content.TryGetValue("operation", out var opEl) && opEl.ValueKind == JsonValueKind.String)
                            operation = opEl.GetString();
                        if (content.TryGetValue("payloadSnippet", out var payEl) && payEl.ValueKind == JsonValueKind.String)
                            payload = payEl.GetString();

                        // Re-categorize with context
                        if (!string.IsNullOrEmpty(operation))
                            category = CategorizeErrorWithContext(errorMessage, operation);
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning("Elicitation not supported in diagnose_error: {Message}", ex.Message);
                }
            }

            // Step 3: Build diagnostic
            var diagnostic = new ErrorDiagnostic
            {
                ErrorMessage = errorMessage,
                ErrorCategory = category,
                PossibleCauses = GetPossibleCauses(category, errorMessage),
                Solutions = GetSolutions(category, errorMessage),
                RelatedFlowRules = string.IsNullOrEmpty(operation)
                    ? new List<string>()
                    : await GetRelatedRulesAsync(operation!, flowService, ct),
                PreventionTips = GetPreventionTips(category),
                Summary = $"Category={category}{(string.IsNullOrEmpty(operation) ? "" : ", op=" + operation)} {BuildInfo.Marker}",
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = operation != null
                            ? $"mcp://stampli-acumatica/validate_request?operation={operation}"
                            : "mcp://stampli-acumatica/query_acumatica_knowledge?query=validation",
                        Name = "Validate request",
                        Description = "Pre-flight validation to prevent this error"
                    },
                    new ResourceLinkBlock
                    {
                        Uri = "mcp://stampli-acumatica/marker",
                        Name = BuildInfo.Marker,
                        Description = $"build={BuildInfo.VersionTag}"
                    }
                }
            };

            // Enrich with operation-specific known errors (if any)
            try
            {
                if (!string.IsNullOrEmpty(operation))
                {
                    var knownErrors = await knowledge.GetOperationErrorsAsync(operation!, ct);
                    foreach (var e in knownErrors)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Message))
                        {
                            diagnostic.PossibleCauses.Add($"Known for {operation}: {e.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Failed to enrich with operation errors: {Message}", ex.Message);
            }

            Serilog.Log.Information("Tool {Tool} completed: category={Category}, solutions={SolutionCount}",
                "diagnose_error", category, diagnostic.Solutions.Count);

            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = diagnostic });
            var summary = diagnostic.Summary ?? $"Diagnostic {BuildInfo.Marker}";
            ret.Content.Add(new TextContentBlock { Type = "text", Text = summary });
            foreach (var link in diagnostic.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed", "diagnose_error");

            var fallback = new ErrorDiagnostic
            {
                ErrorMessage = errorMessage,
                ErrorCategory = "GeneralError",
                PossibleCauses = new List<string> { "Error analysis failed - check error message details" },
                Solutions = new List<ErrorSolution>(),
                Summary = $"GeneralError {BuildInfo.Marker}"
            };
            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = fallback });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = fallback.Summary });
            return ret;
        }
    }

    private static string CategorizeError(string error)
    {
        var lower = error.ToLower();

        // Validation errors
        if (lower.Contains("required") || lower.Contains("missing") || lower.Contains("invalid") ||
            lower.Contains("maximum length") || lower.Contains("exceeds") || lower.Contains("limit") ||
            lower.Contains("field") || lower.Contains("property") || lower.Contains("attribute") ||
            lower.Contains("format") || lower.Contains("must be") || lower.Contains("should be"))
            return "Validation";

        // Not found errors
        if (lower.Contains("not found") || lower.Contains("does not exist") || lower.Contains("cannot find") ||
            lower.Contains("no such") || lower.Contains("404"))
            return "NotFound";

        // Business logic errors
        if (lower.Contains("duplicate") || lower.Contains("already exists") || lower.Contains("business") ||
            lower.Contains("cannot") || lower.Contains("not allowed") || lower.Contains("conflict"))
            return "BusinessLogic";

        // Authentication/Authorization errors
        if (lower.Contains("auth") || lower.Contains("session") || lower.Contains("unauthorized") ||
            lower.Contains("permission") || lower.Contains("access denied") || lower.Contains("forbidden") ||
            lower.Contains("401") || lower.Contains("403"))
            return "Authentication";

        // Rate limiting
        if (lower.Contains("rate limit") || lower.Contains("too many") || lower.Contains("throttle") ||
            lower.Contains("429"))
            return "RateLimit";

        // Network errors
        if (lower.Contains("timeout") || lower.Contains("connection") || lower.Contains("network") ||
            lower.Contains("unreachable") || lower.Contains("503"))
            return "Network";

        // Default to GeneralError instead of Unknown
        return "GeneralError";
    }

    private static string CategorizeErrorWithContext(string error, string operation)
    {
        if (operation.ToLower().Contains("export") || operation.ToLower().Contains("import"))
            return "BusinessLogic";
        return CategorizeError(error);
    }

    private static List<string> GetPossibleCauses(string category, string error)
    {
        return category switch
        {
            "Validation" => new List<string>
            {
                "Missing required field in request payload",
                "Field value exceeds maximum length",
                "Invalid field format (date, number, etc.)",
                "Value outside allowed range or constraints"
            },
            "NotFound" => new List<string>
            {
                "Entity does not exist in Acumatica",
                "Incorrect ID or reference provided",
                "Entity may have been deleted"
            },
            "BusinessLogic" => new List<string>
            {
                "Acumatica business rule violation (e.g., duplicate VendorID)",
                "Related entity not found (e.g., CurrencyID doesn't exist)",
                "Operation not allowed in current state",
                "Conflicting data in related entities"
            },
            "Authentication" => new List<string>
            {
                "Session expired",
                "Invalid credentials",
                "Insufficient permissions",
                "Account locked or disabled"
            },
            "RateLimit" => new List<string>
            {
                "Too many requests in short time",
                "API throttling limit reached",
                "Concurrent request limit exceeded"
            },
            "Network" => new List<string>
            {
                "Connection timeout",
                "Service temporarily unavailable",
                "Network configuration issue"
            },
            "GeneralError" => new List<string>
            {
                "Unexpected error in Acumatica",
                "Internal server error",
                "Configuration issue"
            },
            _ => new List<string> { "Error analysis incomplete - check logs" }
        };
    }

    private static List<ErrorSolution> GetSolutions(string category, string error)
    {
        return category switch
        {
            "Validation" => new List<ErrorSolution>
            {
                new ErrorSolution
                {
                    Description = "Validate request before sending",
                    CodeExample = "await ValidateRequest(operation, payload);",
                    FlowReference = "Use validate_request tool"
                }
            },
            "Authentication" => new List<ErrorSolution>
            {
                new ErrorSolution
                {
                    Description = "Re-authenticate and retry",
                    CodeExample = "await authenticator.LoginAsync();",
                    FlowReference = "STANDARD_IMPORT_FLOW - auth wrapper pattern"
                }
            },
            "NotFound" => new List<ErrorSolution>
            {
                new ErrorSolution
                {
                    Description = "Verify entity exists in Acumatica",
                    CodeExample = "Check entity ID using search operation first",
                    FlowReference = "Use appropriate search operation before referencing"
                }
            },
            "RateLimit" => new List<ErrorSolution>
            {
                new ErrorSolution
                {
                    Description = "Implement exponential backoff",
                    CodeExample = "await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));",
                    FlowReference = "Add retry logic with delays"
                }
            },
            "GeneralError" => new List<ErrorSolution>
            {
                new ErrorSolution
                {
                    Description = "Check Acumatica logs and configuration",
                    CodeExample = "Review error details and Acumatica server logs",
                    FlowReference = "Contact administrator if persists"
                }
            },
            _ => new List<ErrorSolution>()
        };
    }

    private static async Task<List<string>> GetRelatedRulesAsync(string operation, FlowService flowService, CancellationToken ct)
    {
        try
        {
            var flowName = await flowService.GetFlowForOperationAsync(operation, ct);
            if (string.IsNullOrEmpty(flowName)) return new List<string>();
            var doc = await flowService.GetFlowAsync(flowName!, ct);
            if (doc?.RootElement.TryGetProperty("validationRules", out var rulesEl) == true)
            {
                return rulesEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            }
        }
        catch
        {
            // ignore
        }
        return new List<string>();
    }

    private static List<string> GetPreventionTips(string category)
    {
        return category switch
        {
            "Validation" => new List<string>
            {
                "Always use validate_request tool before API calls",
                "Check field length limits in flow documentation"
            },
            "BusinessLogic" => new List<string>
            {
                "Review Acumatica business rules in flow details",
                "Check entity existence before operations"
            },
            _ => new List<string> { "Use diagnose_error tool for detailed analysis" }
        };
    }
}
