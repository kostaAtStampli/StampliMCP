using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class TroubleshootingPrompt
{
    [McpServerPrompt(Name = "debug_with_expert")]
    [Description("Interactive debugging conversation with expert AI. Analyzes errors, identifies root cause, provides fix steps and prevention strategies. Like pair-programming with senior developer during crisis.")]
    public static ChatMessage[] DebugWithExpert(
        [Description("Error message from production or testing (e.g., 'vendorName exceeds maximum length of 60 characters')")]
        string errorMessage)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                """
                You are a senior debugging specialist for Acumatica ERP integrations.
                You excel at root cause analysis, identifying patterns, and providing actionable fixes.
                You communicate clearly, providing both immediate fixes and long-term prevention strategies.
                You reference actual code locations and error catalogs to provide concrete guidance.
                """),

            new ChatMessage(ChatRole.User, $"""
                I'm hitting this error and need your help debugging:

                **Error**: {errorMessage}

                **Available MCP Tools** (unified):
                - `erp__diagnose_error(erp, errorMessage)` – Root cause + solutions
                - `erp__query_knowledge(erp, query, scope?)` – Search operations and flows
                - `erp__get_flow_details(erp, flow)` – Validation rules, constants, critical files

                **Context**:
                - This is from Acumatica integration code (Java or Kotlin)
                - Error could be from validation, API call, or business logic
                - Need to understand why it's happening and how to fix it

                Please help me:
                1. Identify the root cause
                2. Find which operation/field is affected
                3. Provide immediate fix steps
                4. Suggest prevention strategies

                Let's debug this together!
                """),

            new ChatMessage(ChatRole.Assistant, $"""
                I'll help you debug this error. Let me analyze: "{errorMessage}"

                **Initial Assessment**:
                {AnalyzeErrorPattern(errorMessage)}

                **Debugging Strategy**:
                1. First, I'll use `erp__diagnose_error(erp='acumatica', errorMessage=...)` for root cause analysis
                2. Then search knowledge via `erp__query_knowledge(erp='acumatica', query=...)`
                3. Use `erp__get_flow_details` to understand validation context and constants
                4. Provide you with:
                   - Root cause explanation
                   - Affected field/operation
                   - Step-by-step fix instructions
                   - Prevention strategies for the future

                Let me start the analysis...
                """),

            new ChatMessage(ChatRole.User,
                """
                Great approach! Once you have the analysis, please structure your response as:

                **🔍 Root Cause**: What's really happening (not just the symptom)
                **⚠️ Affected Component**: Which operation, field, or module
                **🔧 Immediate Fix**: Step-by-step instructions to resolve now
                **🛡️ Prevention**: How to avoid this in the future (validation, tests, etc.)
                **📚 Related Errors**: Other errors I might encounter in similar situations

                Use the MCP tools to get accurate data from the knowledge base.
                """),

            new ChatMessage(ChatRole.Assistant,
                """
                Perfect! I'll provide a comprehensive debugging report with actionable guidance.
                I'll reference actual code locations from the error catalog so you know exactly where to look.
                Let me gather the detailed analysis now...
                """)
        };
    }

    private static string AnalyzeErrorPattern(string errorMessage)
    {
        var lower = errorMessage.ToLowerInvariant();
        var assessment = new List<string>();

        // Error type identification
        if (lower.Contains("required") || lower.Contains("missing"))
        {
            assessment.Add("**Type**: Missing required field validation error");
            assessment.Add("**Likely cause**: Field not provided in request or null/empty");
        }
        else if (lower.Contains("exceeds") || lower.Contains("maximum") || lower.Contains("length"))
        {
            assessment.Add("**Type**: Field length validation error");
            assessment.Add("**Likely cause**: Input data exceeds maximum allowed length");
        }
        else if (lower.Contains("already exists") || lower.Contains("duplicate"))
        {
            assessment.Add("**Type**: Duplicate/conflict error");
            assessment.Add("**Likely cause**: Attempting to create entity that already exists");
        }
        else if (lower.Contains("not found") || lower.Contains("doesn't exist"))
        {
            assessment.Add("**Type**: Entity not found error");
            assessment.Add("**Likely cause**: Referenced entity doesn't exist in Acumatica");
        }
        else if (lower.Contains("invalid") || lower.Contains("format"))
        {
            assessment.Add("**Type**: Format/validation error");
            assessment.Add("**Likely cause**: Data format doesn't match expected pattern");
        }
        else if (lower.Contains("401") || lower.Contains("unauthorized") || lower.Contains("authentication"))
        {
            assessment.Add("**Type**: Authentication error");
            assessment.Add("**Likely cause**: Invalid credentials or session expired");
        }
        else if (lower.Contains("500") || lower.Contains("internal server"))
        {
            assessment.Add("**Type**: Server error");
            assessment.Add("**Likely cause**: Acumatica server issue or malformed request");
        }
        else
        {
            assessment.Add("**Type**: General error (needs deeper analysis)");
            assessment.Add("**Likely cause**: Will determine from error catalog");
        }

        // Field identification
        var fieldPatterns = new Dictionary<string, string>
        {
            { "vendorname", "VendorName field" },
            { "vendorid", "VendorID field" },
            { "stampliurl", "Stampli link/URL field" },
            { "stamplilink", "Stampli link field" },
            { "email", "Email field" },
            { "amount", "Amount field" },
            { "billid", "Bill ID field" },
            { "paymentid", "Payment ID field" }
        };

        foreach (var (pattern, field) in fieldPatterns)
        {
            if (lower.Contains(pattern))
            {
                assessment.Add($"**Affected field**: {field}");
                break;
            }
        }

        // Operation suggestion
        if (lower.Contains("vendor"))
        {
            assessment.Add("**Suspected operation**: exportVendor or getMatchingVendorByStampliLink");
        }
        else if (lower.Contains("bill") || lower.Contains("invoice"))
        {
            assessment.Add("**Suspected operation**: exportAPTransaction or retrieveInvoiceByReferenceId");
        }
        else if (lower.Contains("payment"))
        {
            assessment.Add("**Suspected operation**: exportBillPayment or retrieveBillPayments");
        }

        return string.Join("\n", assessment);
    }
}
