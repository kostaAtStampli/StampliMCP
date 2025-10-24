using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpRecommendationTool
{
    [McpServerTool(
        Name = "erp__recommend_flow",
        Title = "ERP Flow Recommendation",
        UseStructuredContent = true)]
    [Description("Recommend flows for an ERP using module-provided recommendation services if available.")]
    public static async Task<CallToolResult> Execute(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Use case description for recommendation")] string useCase,
        IMcpServer server,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);
        var recommender = facade.GetService<IErpRecommendationService>();

        FlowRecommendation recommendation;
        if (recommender is null)
        {
            recommendation = new FlowRecommendation
            {
                FlowName = "UNKNOWN",
                Confidence = 0.0,
                Summary = $"ERP '{erp}' does not provide flow recommendations yet",
                Reasoning = "No recommendation engine implemented",
                AlternativeFlows = new List<AlternativeFlow>()
            };
        }
        else
        {
            recommendation = await recommender.RecommendAsync(useCase, ct);
            recommendation.Summary ??= $"Recommendation generated for ERP '{erp}'";
            recommendation.NextActions ??= new List<ResourceLinkBlock>();

            var hasAlternatives = recommendation.AlternativeFlows is { Count: > 0 };
            var lowConfidence = recommendation.Confidence < 0.7;
            var elicitationHandled = false;

            if (lowConfidence || hasAlternatives)
            {
                var fields = new List<Services.ElicitationCompat.Field>();
                if (hasAlternatives)
                {
                    var options = recommendation.AlternativeFlows!
                        .Select(a => a.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (options.Length > 0)
                    {
                        fields.Add(new Services.ElicitationCompat.Field(
                            Name: "choice",
                            Kind: "string",
                            Description: $"Pick a flow ({string.Join(", ", options)}) or leave blank",
                            Options: options));
                    }
                }

                fields.Add(new Services.ElicitationCompat.Field(
                    Name: "clarification",
                    Kind: "string",
                    Description: "Briefly clarify the use case (e.g., 'export POs to Acumatica')"));

                var outcome = await Services.ElicitationCompat.TryElicitAsync(
                    server,
                    "Multiple plausible flows found or low confidence. Choose one or clarify your intent.",
                    fields,
                    ct);

                if (outcome.Supported)
                {
                    Log.Debug("Elicitation for recommend_flow: action={Action}", outcome.Action ?? "none");

                    if (string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase) &&
                        outcome.Content is { } content)
                    {
                        var choice = TryGetString(content, "choice");
                        var clarification = TryGetString(content, "clarification");

                        var refined = string.Join(" ", new[] { useCase, choice, clarification }
                            .Where(s => !string.IsNullOrWhiteSpace(s))!);

                        if (!string.Equals(refined, useCase, StringComparison.Ordinal))
                        {
                            recommendation = await recommender.RecommendAsync(refined, ct);
                            recommendation.Summary ??= $"Refined recommendation for ERP '{erp}'";
                            recommendation.NextActions ??= new List<ResourceLinkBlock>();
                            elicitationHandled = true;
                        }
                    }
                    else
                    {
                        Log.Debug("Elicitation declined or no refinement provided.");
                    }
                }
            }

            EnsureLowConfidenceFallbacks(recommendation, erp, useCase, lowConfidence, elicitationHandled);
        }

        recommendation.NextActions.Add(new ResourceLinkBlock
        {
            Uri = $"mcp://stampli-unified/erp__list_flows?erp={erp}",
            Name = "Browse flows"
        });

        var callResult = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = recommendation })
        };

        var json = JsonSerializer.Serialize(recommendation, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        callResult.Content.Add(new TextContentBlock { Type = "text", Text = json });
        foreach (var link in recommendation.NextActions)
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }

    private static string? TryGetString(IReadOnlyDictionary<string, JsonElement> content, string key)
    {
        if (content.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static void EnsureLowConfidenceFallbacks(FlowRecommendation recommendation, string erp, string useCase, bool lowConfidence, bool elicitationHandled)
    {
        if (!lowConfidence || elicitationHandled)
        {
            return;
        }

        recommendation.NextActions ??= new List<ResourceLinkBlock>();

        var normalized = useCase.ToLowerInvariant();
        var tokens = normalized.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var mentionsPo = normalized.Contains("purchase order", StringComparison.OrdinalIgnoreCase)
            || tokens.Contains("po", StringComparer.OrdinalIgnoreCase)
            || tokens.Contains("purchase", StringComparer.OrdinalIgnoreCase) && tokens.Contains("order", StringComparer.OrdinalIgnoreCase);

        var altMentionsPo = recommendation.AlternativeFlows?.Any(a =>
            a.Name.Contains("purchase", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("po", StringComparison.OrdinalIgnoreCase)) ?? false;

        if (!mentionsPo && !altMentionsPo)
        {
            return;
        }

        AddLink(
            recommendation.NextActions,
            $"mcp://stampli-unified/erp__recommend_flow?erp={erp}&useCase={Uri.EscapeDataString("export purchase orders to Acumatica")}",
            "Refine: export purchase orders",
            "Focus on exporting purchase orders from Stampli to Acumatica.");

        AddLink(
            recommendation.NextActions,
            $"mcp://stampli-unified/erp__recommend_flow?erp={erp}&useCase={Uri.EscapeDataString("import purchase orders for matching")}",
            "Refine: PO matching import",
            "Look up purchase orders and receipts for PO matching workflows.");

        AddLink(
            recommendation.NextActions,
            $"mcp://stampli-unified/erp__recommend_flow?erp={erp}&useCase={Uri.EscapeDataString("import all purchase orders and closed PRs")}",
            "Refine: bulk PO sync",
            "Bulk import open/closed purchase orders and purchase receipts.");

        AddLink(
            recommendation.NextActions,
            $"mcp://stampli-unified/erp__recommend_flow?erp={erp}&useCase={Uri.EscapeDataString("standard import purchase order data")}",
            "Refine: standard import",
            "Fallback to generic purchase-order import flow.");
    }

    private static void AddLink(ICollection<ResourceLinkBlock> links, string uri, string name, string? description)
    {
        if (links.Any(l => string.Equals(l.Uri, uri, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        links.Add(new ResourceLinkBlock
        {
            Uri = uri,
            Name = name,
            Description = description
        });
    }
}

