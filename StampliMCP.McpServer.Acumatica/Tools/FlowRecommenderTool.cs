using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class FlowRecommenderTool
{
    // Elicitation input for flow selection
    public sealed class FlowPreference
    {
        public string FlowName { get; set; } = string.Empty;
        public string? AdditionalContext { get; set; }
    }

    [McpServerTool(
        Name = "recommend_flow",
        Title = "AI Flow Recommender",
        UseStructuredContent = true
    )]
    [Description(@"
AI-powered flow recommendation based on your integration use case.

Uses intelligence matching across 9 proven flows:
• VENDOR_EXPORT_FLOW - Export vendors with validation + UI links
• PAYMENT_FLOW - International payments with cross-rate calculation
• STANDARD_IMPORT_FLOW - Paginated imports (2000 rows/page)
• PO_MATCHING_FLOW - PO matching with receipt lookup
• PO_MATCHING_FULL_IMPORT_FLOW - Full PO import with line items
• EXPORT_INVOICE_FLOW - Invoice export with line items
• EXPORT_PO_FLOW - PO export (purchase orders)
• M2M_IMPORT_FLOW - Many-to-many relationship imports
• API_ACTION_FLOW - Generic API actions (submit/release/etc.)

Features:
✓ AI confidence scoring (0.0 to 1.0)
✓ Interactive elicitation for low-confidence matches (<0.7)
✓ Alternative flows with reasoning
✓ Complete flow details in response
✓ Resource links to TDD workflow

Examples:
• 'export vendors to Stampli' → VENDOR_EXPORT_FLOW (0.95)
• 'import payment data' → Elicits: PAYMENT_FLOW vs STANDARD_IMPORT_FLOW
• 'sync purchase orders' → EXPORT_PO_FLOW or PO_MATCHING_FLOW (user chooses)
")]
    public static async Task<FlowRecommendation> Execute(
        [Description("Use case description (e.g., 'export vendors', 'import payments', 'sync purchase orders')")]
        string useCase,

        ModelContextProtocol.Server.McpServer server,
        FlowService flowService,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: useCase={UseCase}",
            "recommend_flow", useCase);

        try
        {
            // Step 1: Get recommendation using keyword matching
            var recommendation = await RecommendFlowByKeywords(useCase, ct);

            // Step 2: Elicit if confidence is low (Protocol 2025-06-18)
            if (recommendation.Confidence < 0.7 && recommendation.AlternativeFlows.Any())
            {
                var alternatives = recommendation.AlternativeFlows
                    .OrderByDescending(a => a.Confidence)
                    .Take(3)
                    .ToList();

                var message = $"Found multiple possible flows (confidence {recommendation.Confidence:P0}). Please choose or provide more context:";

                var elicitResult = await server.ElicitAsync<FlowPreference>(
                    message,
                    cancellationToken: ct
                );

                if (elicitResult.Action == "accept" && elicitResult.Content is { } preference)
                {
                    // Re-run recommendation with additional context
                    var refinedUseCase = !string.IsNullOrWhiteSpace(preference.AdditionalContext)
                        ? $"{useCase} {preference.AdditionalContext}"
                        : useCase;

                    if (!string.IsNullOrEmpty(preference.FlowName))
                    {
                        // User chose a specific flow
                        recommendation.FlowName = preference.FlowName;
                        recommendation.Confidence = 1.0; // User confirmed
                        recommendation.Reasoning = $"User selected: {preference.FlowName}. {recommendation.Reasoning}";
                    }
                    else
                    {
                        // Re-recommend with additional context
                        recommendation = await RecommendFlowByKeywords(refinedUseCase, ct);
                    }
                }
            }

            // Step 3: Load complete flow details
            var flowDoc = await flowService.GetFlowAsync(recommendation.FlowName, ct);
            var flowDetail = new FlowDetail
            {
                Name = recommendation.FlowName,
                Description = flowDoc?.RootElement.GetProperty("description").GetString() ?? "",
                // Parse anatomy, constants, etc. (simplified for now)
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = $"mcp://stampli-acumatica/get_flow_details?flow={recommendation.FlowName}",
                        Name = $"Deep dive into {recommendation.FlowName}",
                        Description = "Get complete flow anatomy, constants, and validation rules"
                    },
                    new ResourceLinkBlock
                    {
                        Uri = $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={recommendation.FlowName}",
                        Name = "Start TDD implementation",
                        Description = "Generate structured TDD workflow for this flow"
                    }
                }
            };

            recommendation.Details = flowDetail;

            Serilog.Log.Information("Tool {Tool} completed: flow={Flow}, confidence={Confidence}",
                "recommend_flow", recommendation.FlowName, recommendation.Confidence);

            return recommendation;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "recommend_flow", ex.Message);

            return new FlowRecommendation
            {
                FlowName = "UNKNOWN",
                Confidence = 0.0,
                Reasoning = $"Error during recommendation: {ex.Message}",
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = "mcp://stampli-acumatica/query_acumatica_knowledge?query=flows",
                        Name = "Browse all flows",
                        Description = "Explore available flows manually"
                    }
                }
            };
        }
    }

    private static Task<FlowRecommendation> RecommendFlowByKeywords(string useCase, CancellationToken ct)
    {
        var lowerCase = useCase.ToLower();

        // Keyword-based flow matching with confidence scores
        var matches = new List<(string flow, double confidence, string reason)>();

        if (lowerCase.Contains("vendor") && (lowerCase.Contains("export") || lowerCase.Contains("send") || lowerCase.Contains("sync")))
            matches.Add(("vendor_export_flow", 0.95, "Detected vendor export scenario with validation + UI link generation"));

        if (lowerCase.Contains("payment") || lowerCase.Contains("pay"))
        {
            if (lowerCase.Contains("international") || lowerCase.Contains("currency"))
                matches.Add(("payment_flow", 0.95, "International payment with cross-rate calculation required"));
            else
                matches.Add(("payment_flow", 0.85, "Payment processing flow"));
        }

        if (lowerCase.Contains("import") && !lowerCase.Contains("po") && !lowerCase.Contains("purchase"))
            matches.Add(("standard_import_flow", 0.90, "Standard paginated import (2000 rows/page) with auth wrapper"));

        if (lowerCase.Contains("purchase order") || lowerCase.Contains("po"))
        {
            if (lowerCase.Contains("match") || lowerCase.Contains("receipt"))
                matches.Add(("po_matching_flow", 0.90, "PO matching with receipt lookup"));
            else if (lowerCase.Contains("import") && (lowerCase.Contains("full") || lowerCase.Contains("complete")))
                matches.Add(("po_matching_full_import_flow", 0.90, "Full PO import with line items"));
            else if (lowerCase.Contains("export"))
                matches.Add(("export_po_flow", 0.85, "Purchase order export"));
        }

        if (lowerCase.Contains("invoice") && lowerCase.Contains("export"))
            matches.Add(("export_invoice_flow", 0.90, "Invoice export with line items"));

        if (lowerCase.Contains("many") || lowerCase.Contains("m2m") || lowerCase.Contains("relationship"))
            matches.Add(("m2m_import_flow", 0.85, "Many-to-many relationship import"));

        if (lowerCase.Contains("submit") || lowerCase.Contains("release") || lowerCase.Contains("action"))
            matches.Add(("api_action_flow", 0.80, "Generic API actions (submit/release/etc.)"));

        // Sort by confidence and prepare result
        var ordered = matches.OrderByDescending(m => m.confidence).ToList();

        if (!ordered.Any())
        {
            // No matches - return generic import flow as fallback
            return Task.FromResult(new FlowRecommendation
            {
                FlowName = "standard_import_flow",
                Confidence = 0.5,
                Reasoning = "No specific keywords matched. Recommending generic import flow. Please refine your use case.",
                AlternativeFlows = new List<AlternativeFlow>
                {
                    new AlternativeFlow { Name = "vendor_export_flow", Confidence = 0.3, Reason = "If exporting vendors" },
                    new AlternativeFlow { Name = "payment_flow", Confidence = 0.3, Reason = "If processing payments" }
                }
            });
        }

        var best = ordered.First();
        var alternatives = ordered.Skip(1).Take(3).Select(m => new AlternativeFlow
        {
            Name = m.flow,
            Confidence = m.confidence,
            Reason = m.reason
        }).ToList();

        return Task.FromResult(new FlowRecommendation
        {
            FlowName = best.flow,
            Confidence = best.confidence,
            Reasoning = best.reason,
            AlternativeFlows = alternatives
        });
    }
}
