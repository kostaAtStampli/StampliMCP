using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

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
    public static async Task<ErrorDiagnostic> Execute(
        [Description("Error message from Acumatica API")]
        string errorMessage,

        ModelContextProtocol.Server.McpServer server,
        FlowService flowService,
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
                var elicitResult = await server.ElicitAsync<ErrorContext>(
                    "Need more context to diagnose. Please provide operation and/or payload snippet:",
                    cancellationToken: ct
                );

                if (elicitResult.Action == "accept" && elicitResult.Content is { } context)
                {
                    operation = context.Operation;
                    payload = context.PayloadSnippet;

                    // Re-categorize with context
                    if (!string.IsNullOrEmpty(operation))
                        category = CategorizeErrorWithContext(errorMessage, operation);
                }
            }

            // Step 3: Build diagnostic
            var diagnostic = new ErrorDiagnostic
            {
                ErrorMessage = errorMessage,
                ErrorCategory = category,
                PossibleCauses = GetPossibleCauses(category, errorMessage),
                Solutions = GetSolutions(category, errorMessage),
                RelatedFlowRules = await GetRelatedRules(operation, flowService, ct),
                PreventionTips = GetPreventionTips(category),
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = operation != null
                            ? $"mcp://stampli-acumatica/validate_request?operation={operation}"
                            : "mcp://stampli-acumatica/query_acumatica_knowledge?query=validation",
                        Name = "Validate request",
                        Description = "Pre-flight validation to prevent this error"
                    }
                }
            };

            Serilog.Log.Information("Tool {Tool} completed: category={Category}, solutions={SolutionCount}",
                "diagnose_error", category, diagnostic.Solutions.Count);

            return diagnostic;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed", "diagnose_error");

            return new ErrorDiagnostic
            {
                ErrorMessage = errorMessage,
                ErrorCategory = "Unknown",
                PossibleCauses = new List<string> { "Unable to analyze error" },
                Solutions = new List<ErrorSolution>()
            };
        }
    }

    private static string CategorizeError(string error)
    {
        var lower = error.ToLower();
        if (lower.Contains("required") || lower.Contains("missing") || lower.Contains("invalid"))
            return "Validation";
        if (lower.Contains("duplicate") || lower.Contains("exists") || lower.Contains("business"))
            return "BusinessLogic";
        if (lower.Contains("auth") || lower.Contains("session") || lower.Contains("unauthorized"))
            return "Authentication";
        if (lower.Contains("timeout") || lower.Contains("connection"))
            return "Network";
        return "Unknown";
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
                "Invalid field format (date, number, etc.)"
            },
            "BusinessLogic" => new List<string>
            {
                "Acumatica business rule violation (e.g., duplicate VendorID)",
                "Related entity not found (e.g., CurrencyID doesn't exist)",
                "Operation not allowed in current state"
            },
            "Authentication" => new List<string>
            {
                "Session expired",
                "Invalid credentials",
                "Insufficient permissions"
            },
            _ => new List<string> { "Unknown cause - need more context" }
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
            _ => new List<ErrorSolution>()
        };
    }

    private static async Task<List<string>> GetRelatedRules(string? operation, FlowService flowService, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(operation))
            return new List<string>();

        // Simplified - in real impl, look up operation's flow and return its rules
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
