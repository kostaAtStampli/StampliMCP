using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class AnalyzeIntegrationPrompt
{
    [McpServerPrompt(Name = "analyze_integration_strategy")]
    [Description("Strategic planning conversation for complex integrations. AI acts as solutions architect, breaking down features into operations, estimating effort, identifying risks. More engaging than tool output.")]
    public static ChatMessage[] AnalyzeIntegrationStrategy(
        [Description("Integration requirement in business language (e.g., 'Vendor payment approval workflow with multi-level approvals')")]
        string requirement)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                """
                You are a senior solutions architect specializing in Acumatica ERP integrations.
                You excel at breaking down complex business requirements into technical implementation plans.
                You estimate effort accurately by analyzing operation complexity, dependencies, and risks.
                You communicate in business-friendly language while providing technical depth when needed.
                """),

            new ChatMessage(ChatRole.User, $"""
                I need you to analyze this integration requirement and provide a strategic implementation plan:

                **Requirement**: {requirement}

                **Available MCP Tools**:
                - `analyze_integration_complexity` - Get AI-powered complexity analysis
                - `search_operations` - Find all 51 available Acumatica operations
                - `get_operation_details` - Get detailed specs for specific operations

                **Analysis Framework**:
                Please provide:
                1. **Operations Breakdown** - Which Acumatica operations are needed and why
                2. **Complexity Assessment** - Simple/Medium/Complex with reasoning
                3. **Effort Estimation** - Hours/days with breakdown by operation
                4. **Implementation Order** - Which operations to implement first (dependencies)
                5. **Risk Analysis** - Technical risks, data risks, integration risks
                6. **Success Criteria** - How to verify the integration works correctly

                Start by analyzing the requirement and identifying key components.
                """),

            new ChatMessage(ChatRole.Assistant, $"""
                Let me analyze "{requirement}" from an architectural perspective.

                **Initial Analysis**:
                - Business goal: {IdentifyBusinessGoal(requirement)}
                - Key entities involved: {IdentifyEntities(requirement)}
                - Integration pattern: {IdentifyPattern(requirement)}
                - Estimated operations needed: {EstimateOperationCount(requirement)}

                Let me use the MCP tools to get detailed analysis:
                1. First, I'll search for operations matching key entities
                2. Then analyze complexity using `analyze_integration_complexity`
                3. Finally, drill into specific operations with `get_operation_details`

                Starting analysis now...
                """),

            new ChatMessage(ChatRole.User,
                """
                Perfect approach! After you gather the technical details, please synthesize into:

                **Executive Summary**: 2-3 sentences for management
                **Technical Breakdown**: Detailed operation analysis for developers
                **Timeline Estimate**: Realistic timeframe with phases
                **Risk Mitigation**: Specific strategies for identified risks

                Make sure to call the MCP tools to get accurate operation data.
                """),

            new ChatMessage(ChatRole.Assistant,
                """
                Understood! I'll provide both business-level summary and technical depth.
                I'll use real operation data from MCP rather than assumptions.
                Let me gather the necessary details now...
                """)
        };
    }

    private static string IdentifyBusinessGoal(string requirement)
    {
        var lower = requirement.ToLowerInvariant();

        if (lower.Contains("approval") && lower.Contains("workflow"))
            return "Implement multi-stage approval process";
        if (lower.Contains("bulk") || lower.Contains("batch"))
            return "Enable bulk/batch processing";
        if (lower.Contains("import") && lower.Contains("export"))
            return "Bidirectional data synchronization";
        if (lower.Contains("payment"))
            return "Payment processing and tracking";
        if (lower.Contains("vendor") && lower.Contains("management"))
            return "Vendor lifecycle management";
        if (lower.Contains("bill") || lower.Contains("invoice"))
            return "Bill/invoice processing";

        return "Custom ERP integration";
    }

    private static string IdentifyEntities(string requirement)
    {
        var entities = new List<string>();
        var lower = requirement.ToLowerInvariant();

        if (lower.Contains("vendor")) entities.Add("Vendor");
        if (lower.Contains("bill") || lower.Contains("invoice")) entities.Add("Bill/Invoice");
        if (lower.Contains("payment")) entities.Add("Payment");
        if (lower.Contains("approval")) entities.Add("Approval");
        if (lower.Contains("customer")) entities.Add("Customer");
        if (lower.Contains("document")) entities.Add("Document");

        return entities.Count > 0 ? string.Join(", ", entities) : "TBD (will analyze)";
    }

    private static string IdentifyPattern(string requirement)
    {
        var lower = requirement.ToLowerInvariant();

        if (lower.Contains("workflow") && lower.Contains("approval"))
            return "Workflow orchestration with state management";
        if (lower.Contains("bulk") || lower.Contains("import"))
            return "Bulk import with pagination";
        if ((lower.Contains("create") || lower.Contains("export")) && lower.Contains("duplicate"))
            return "Idempotent create-or-update";
        if (lower.Contains("sync") || lower.Contains("bidirectional"))
            return "Bidirectional sync";
        if (lower.Contains("real-time") || lower.Contains("realtime"))
            return "Real-time event-driven";

        return "Request-response CRUD";
    }

    private static string EstimateOperationCount(string requirement)
    {
        var lower = requirement.ToLowerInvariant();
        var count = 1; // Base operation

        if (lower.Contains("approval") || lower.Contains("workflow")) count += 2; // Multiple states
        if (lower.Contains("bulk")) count += 1; // Pagination operation
        if (lower.Contains("duplicate") || lower.Contains("check")) count += 1; // Lookup operation
        if (lower.Contains("payment") && lower.Contains("bill")) count += 2; // Bill + payment ops
        if (lower.Contains("vendor") && (lower.Contains("import") || lower.Contains("export"))) count += 2; // Import + export

        return $"{count}-{count + 2} operations";
    }
}
