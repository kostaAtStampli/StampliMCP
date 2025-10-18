using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class KotlinFeaturePrompt
{
    [McpServerPrompt(Name = "implement_feature_guided")]
    [Description("Interactive TDD workflow conversation that guides AI through implementing Kotlin feature step-by-step. More engaging than tool - AI feels like it's pair-programming with senior developer.")]
    public static ChatMessage[] ImplementFeatureGuided(
        [Description("Feature description in natural language (e.g., 'Add vendor bulk import with CSV validation')")]
        string featureDescription)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                """
                You are an expert Kotlin developer specializing in Acumatica ERP integrations.
                You follow strict Test-Driven Development (TDD) methodology.
                You implement features by following a 7-step enforced workflow.
                You NEVER skip tests. You ALWAYS verify RED phase before implementing.
                You use exact error messages from legacy code. You NEVER throw exceptions for business logic.
                """),

            new ChatMessage(ChatRole.User, $"""
                I need you to implement this feature using TDD:

                **Feature**: {featureDescription}

                **Available MCP Tools**:
                - `search_operations` - Find relevant Acumatica operations
                - `get_operation_details` - Get detailed operation specs (fields, errors, file pointers)
                - File reading tools - Scan legacy Java code
                - File writing tools - Create Kotlin tests and implementation
                - Bash tools - Run tests and verify RED/GREEN phases

                **Knowledge Base**:
                - Knowledge/kotlin/GOLDEN_PATTERNS.md - Copy-paste implementation patterns
                - Knowledge/kotlin/error-patterns-kotlin.json - Exact error messages
                - Knowledge/kotlin/TDD_WORKFLOW.md - TDD methodology
                - Knowledge/operations/*.json - All 51 operations with field specs

                **Test Instance**:
                - URL: http://63.32.187.185/StampliAcumaticaDB
                - User: admin / Password1
                - Use timestamp-based unique data to avoid conflicts

                **Critical Rules**:
                1. Tests MUST fail first (RED phase) - if they pass, something is wrong
                2. Use EXACT error messages from get_operation_details (not paraphrased)
                3. ALWAYS use AcumaticaAuthenticator.authenticatedApiCall for API calls
                4. NEVER throw exceptions for validation errors - set response.error instead
                5. Follow patterns from GOLDEN_PATTERNS.md exactly

                Please start by discovering which operations are needed for this feature.
                """),

            new ChatMessage(ChatRole.Assistant,
                $"""
                I'll implement "{featureDescription}" using enforced TDD workflow. Let me break this down:

                **Step 1: Discover Operations**
                I need to identify which Acumatica operations are required. Let me search...

                I'm going to use `search_operations` to find relevant operations based on keywords in the feature description.

                **Analysis**:
                - Feature involves: {AnalyzeFeatureKeywords(featureDescription)}
                - Likely operations needed: {SuggestOperations(featureDescription)}
                - Will query each operation for detailed specs

                Starting discovery now...
                """),

            new ChatMessage(ChatRole.User,
                """
                Good! After you've identified operations, follow this sequence:

                **Step 2**: Call `get_operation_details` for each operation
                **Step 3**: Read legacy code files (use file pointers from Step 2)
                **Step 4**: Write tests FIRST (TDD RED phase)
                **Step 5**: Run tests - VERIFY they FAIL
                **Step 6**: Implement Kotlin code following GOLDEN_PATTERNS.md
                **Step 7**: Run tests - VERIFY they PASS

                Remember: If tests pass in Step 5, STOP - something is wrong!
                """),

            new ChatMessage(ChatRole.Assistant,
                """
                Understood! I'll follow the strict TDD workflow:
                ✓ Discover → Query → Scan → Test(FAIL) → Implement → Test(PASS) → Report

                Let me proceed with operation discovery and details retrieval...
                """)
        };
    }

    private static string AnalyzeFeatureKeywords(string feature)
    {
        var lower = feature.ToLowerInvariant();
        var keywords = new List<string>();

        if (lower.Contains("vendor")) keywords.Add("vendor operations");
        if (lower.Contains("bill") || lower.Contains("invoice")) keywords.Add("bill operations");
        if (lower.Contains("payment")) keywords.Add("payment operations");
        if (lower.Contains("bulk") || lower.Contains("import")) keywords.Add("bulk import/pagination");
        if (lower.Contains("export") || lower.Contains("create")) keywords.Add("export/create operations");
        if (lower.Contains("validation")) keywords.Add("validation logic");
        if (lower.Contains("duplicate")) keywords.Add("duplicate checking");

        return keywords.Count > 0 ? string.Join(", ", keywords) : "general ERP operations";
    }

    private static string SuggestOperations(string feature)
    {
        var lower = feature.ToLowerInvariant();
        var ops = new List<string>();

        if (lower.Contains("vendor"))
        {
            if (lower.Contains("export") || lower.Contains("create")) ops.Add("exportVendor");
            if (lower.Contains("import") || lower.Contains("get")) ops.Add("getVendors");
            if (lower.Contains("duplicate") || lower.Contains("check")) ops.Add("getMatchingVendorByStampliLink");
        }

        if (lower.Contains("bill") || lower.Contains("invoice"))
        {
            if (lower.Contains("export")) ops.Add("exportAPTransaction");
            if (lower.Contains("import")) ops.Add("getPaidBills");
        }

        if (lower.Contains("payment"))
        {
            ops.Add("exportBillPayment");
            ops.Add("retrieveBillPayments");
        }

        return ops.Count > 0 ? string.Join(", ", ops) : "TBD (will search)";
    }
}
