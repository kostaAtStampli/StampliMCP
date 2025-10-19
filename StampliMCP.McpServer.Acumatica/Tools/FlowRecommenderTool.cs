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
    public static async Task<CallToolResult> Execute(
        [Description("Use case description (e.g., 'export vendors', 'import payments', 'sync purchase orders')")]
        string useCase,

        ModelContextProtocol.Server.McpServer server,
        FlowService flowService,
        SmartFlowMatcher smartFlowMatcher,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: useCase={UseCase}",
            "recommend_flow", useCase);

        try
        {
            // Step 1: Get recommendation using keyword matching
            var recommendation = await RecommendFlowByKeywords(useCase, smartFlowMatcher, ct);

            // Step 2: Try elicitation if confidence is low (Protocol 2025-06-18)
            // If elicitation is not supported, continue with best guess
            if (recommendation.Confidence < 0.7 && recommendation.AlternativeFlows.Any())
            {
                try
                {
                    var alternatives = recommendation.AlternativeFlows
                        .OrderByDescending(a => a.Confidence)
                        .Take(3)
                        .ToList();

                    var message = $"Found multiple possible flows (confidence {recommendation.Confidence:P0}). Please choose or provide more context:";

                    var schema = new ElicitRequestParams.RequestSchema
                    {
                        Properties =
                        {
                            ["flowName"] = new ElicitRequestParams.StringSchema
                            {
                                Description = $"Choose a flow ({string.Join(", ", alternatives.Select(a => a.Name))})"
                            },
                            ["additionalContext"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "More context to refine recommendation"
                            }
                        }
                    };

                    var elicitResult = await server.ElicitAsync(new ElicitRequestParams
                    {
                        Message = message,
                        RequestedSchema = schema
                    }, ct);

                    if (elicitResult.Action == "accept" && elicitResult.Content is { } content)
                    {
                        string? chosen = null;
                        string? additional = null;

                        if (content.TryGetValue("flowName", out var fEl) && fEl.ValueKind == JsonValueKind.String)
                            chosen = fEl.GetString();
                        if (content.TryGetValue("additionalContext", out var cEl) && cEl.ValueKind == JsonValueKind.String)
                            additional = cEl.GetString();

                        // Re-run recommendation with additional context
                        var refinedUseCase = !string.IsNullOrWhiteSpace(additional)
                            ? $"{useCase} {additional}"
                            : useCase;

                        if (!string.IsNullOrEmpty(chosen))
                        {
                            // User chose a specific flow
                            recommendation.FlowName = chosen;
                            recommendation.Confidence = 1.0; // User confirmed
                            recommendation.Reasoning = $"User selected: {chosen}. {recommendation.Reasoning}";
                        }
                        else
                        {
                            // Re-recommend with additional context
                            recommendation = await RecommendFlowByKeywords(refinedUseCase, smartFlowMatcher, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Elicitation not supported - continue with best guess
                    Serilog.Log.Warning("Elicitation not supported: {Message}. Using best guess flow.", ex.Message);
                    recommendation.Reasoning += " (Note: Multiple flows matched. Consider being more specific.)";
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
            recommendation.Summary = $"Recommend {recommendation.FlowName} (confidence {recommendation.Confidence:P0}) {BuildInfo.Marker}";

            // Append temporary verification marker in next actions
            recommendation.NextActions.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-acumatica/marker",
                Name = BuildInfo.Marker,
                Description = $"build={BuildInfo.VersionTag}"
            });

            Serilog.Log.Information("Tool {Tool} completed: flow={Flow}, confidence={Confidence}",
                "recommend_flow", recommendation.FlowName, recommendation.Confidence);

            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = recommendation });
            
            // Serialize full recommendation as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(recommendation, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
            
            foreach (var link in recommendation.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "recommend_flow", ex.Message);

            var fallback = new FlowRecommendation
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
                },
                Summary = $"Recommendation unavailable. {BuildInfo.Marker}"
            };
            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = fallback });
            
            // Serialize fallback as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(fallback, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
            
            foreach (var link in fallback.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
            return ret;
        }
    }

    private static Task<FlowRecommendation> RecommendFlowByKeywords(string useCase, SmartFlowMatcher smartFlowMatcher, CancellationToken ct)
    {
        // Use smart matcher with SearchValues (7x faster than Contains)
        var analysis = smartFlowMatcher.AnalyzeQuery(useCase);
        var matches = new List<(string flow, double confidence, string reason)>();

        // Extract flags from analysis
        var hasImport = analysis.Actions.Any(a => a is "import" or "get" or "fetch" or "retrieve");
        var hasExport = analysis.Actions.Any(a => a is "export" or "send" or "create" or "sync");
        var hasSubmit = analysis.Actions.Any(a => a is "submit" or "release");

        var hasVendor = analysis.Entities.Contains("vendor");
        var hasInvoice = analysis.Entities.Contains("invoice");
        var hasPayment = analysis.Entities.Contains("payment");
        var hasItem = analysis.Entities.Contains("item");
        var hasPO = analysis.Entities.Any(e => e is "po" or "purchase" or "order");
        var hasTransaction = analysis.Entities.Contains("transaction");

        // Check for special keywords in original words
        var hasMatch = analysis.Words.Any(w => w.Contains("match"));
        var hasReceipt = analysis.Words.Any(w => w.Contains("receipt"));
        var hasFull = analysis.Words.Any(w => w is "full" or "complete" or "all");
        var hasInternational = analysis.Words.Any(w => w is "international" or "currency" or "multicurrency");
        var hasM2M = analysis.Words.Any(w => w is "many" or "m2m" or "relationship");

        // Smart entity-specific matching (fixes "import items" bug!)
        if (hasImport && hasItem)
            matches.Add(("standard_import_flow", 0.95, "Import items from Acumatica"));

        if (hasImport && hasVendor)
            matches.Add(("standard_import_flow", 0.95, "Import vendors from Acumatica"));

        if (hasExport && hasVendor)
            matches.Add(("vendor_export_flow", 0.95, "Export/create vendors in Acumatica with validation + UI links"));

        if (hasPayment)
        {
            if (hasInternational)
                matches.Add(("payment_flow", 0.95, "International payment with cross-rate calculation"));
            else if (hasExport)
                matches.Add(("payment_flow", 0.90, "Export bill payments to Acumatica"));
            else if (hasImport)
                matches.Add(("standard_import_flow", 0.85, "Import payment data"));
            else
                matches.Add(("payment_flow", 0.85, "Payment processing flow"));
        }

        if (hasInvoice || hasTransaction)
        {
            if (hasExport)
                matches.Add(("export_invoice_flow", 0.95, "Export bills/invoices to Acumatica with validation"));
            else if (hasImport)
                matches.Add(("standard_import_flow", 0.85, "Import invoice/transaction data"));
        }

        if (hasPO)
        {
            if (hasMatch || hasReceipt)
                matches.Add(("po_matching_flow", 0.95, "PO matching with receipt lookup for 3-way matching"));
            else if (hasImport && hasFull)
                matches.Add(("po_matching_full_import_flow", 0.90, "Full PO import with line items"));
            else if (hasExport)
                matches.Add(("export_po_flow", 0.90, "Export purchase orders to Acumatica"));
            else if (hasImport)
                matches.Add(("standard_import_flow", 0.85, "Import purchase order data"));
        }

        if (hasM2M)
            matches.Add(("m2m_import_flow", 0.85, "Many-to-many relationship import (Branch→Project→Task)"));

        if (hasSubmit)
            matches.Add(("api_action_flow", 0.85, "API actions (submit/release/void operations)"));

        // Generic import/export fallbacks
        if (!matches.Any())
        {
            if (hasImport)
                matches.Add(("standard_import_flow", 0.70, "Generic paginated import (2000 rows/page)"));
            else if (hasExport)
                matches.Add(("standard_import_flow", 0.60, "No specific export flow matched - try refining query"));
        }

        // Typo tolerance - check common queries with Levenshtein distance
        if (!matches.Any() || matches.Max(m => m.confidence) < 0.7)
        {
            var typoMatches = CheckTypoTolerance(useCase, smartFlowMatcher);
            matches.AddRange(typoMatches);
        }

        // Sort by confidence
        var ordered = matches.OrderByDescending(m => m.confidence).ToList();

        if (!ordered.Any())
        {
            // No matches at all - return helpful choices
            return Task.FromResult(new FlowRecommendation
            {
                FlowName = "UNKNOWN",
                Confidence = 0.0,
                Reasoning = "Could not match your request. Please choose one of these:\n" +
                           "• vendor_export_flow - Create vendors in Acumatica\n" +
                           "• standard_import_flow - Import vendors, items, accounts, etc.\n" +
                           "• payment_flow - Process payments\n" +
                           "• export_invoice_flow - Export bills/invoices\n" +
                           "• export_po_flow - Export purchase orders\n\n" +
                           "Or use: mcp__stampli-acumatica__list_flows to see all 9 flows",
                AlternativeFlows = new List<AlternativeFlow>
                {
                    new AlternativeFlow { Name = "vendor_export_flow", Confidence = 0.3, Reason = "For vendor operations" },
                    new AlternativeFlow { Name = "standard_import_flow", Confidence = 0.3, Reason = "For importing data" },
                    new AlternativeFlow { Name = "payment_flow", Confidence = 0.3, Reason = "For payments" }
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
            Reasoning = best.reason + (best.confidence < 0.9 ? " (Note: Multiple flows matched. Consider being more specific.)" : ""),
            AlternativeFlows = alternatives
        });
    }

    private static List<(string flow, double confidence, string reason)> CheckTypoTolerance(string query, SmartFlowMatcher smartFlowMatcher)
    {
        var matches = new List<(string flow, double confidence, string reason)>();

        // Common query patterns to check against (pattern, flow, reason)
        var commonPatterns = new List<(string pattern, string flow, string reason)>
        {
            ("import items", "standard_import_flow", "Import items from Acumatica (typo corrected)"),
            ("import vendors", "standard_import_flow", "Import vendors from Acumatica (typo corrected)"),
            ("export vendor", "vendor_export_flow", "Export vendors to Acumatica (typo corrected)"),
            ("export vendors", "vendor_export_flow", "Export vendors to Acumatica (typo corrected)"),
            ("import payments", "standard_import_flow", "Import payment data (typo corrected)"),
            ("export invoice", "export_invoice_flow", "Export invoices to Acumatica (typo corrected)"),
            ("export payment", "payment_flow", "Export bill payments (typo corrected)")
        };

        // OPTIMAL FASTENSHTEIN: Create ONE instance with query, compare against ALL patterns
        var confidence = smartFlowMatcher.CalculateTypoDistance(query, commonPatterns.Select(p => p.pattern));

        // Find best matching pattern
        foreach (var (pattern, flow, reason) in commonPatterns)
        {
            if (pattern.Equals(query, StringComparison.OrdinalIgnoreCase) || confidence >= 0.70) // 70% threshold (generous)
            {
                matches.Add((flow, confidence, reason));
            }
        }

        return matches;
    }
}
