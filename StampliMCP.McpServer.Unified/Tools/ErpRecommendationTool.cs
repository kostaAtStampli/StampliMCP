using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
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


            // Opportunistic elicitation: ask user to pick/clarify when confidence is low
            var hasAlternatives = recommendation.AlternativeFlows is { Count: > 0 };
            if ((recommendation.Confidence < 0.7 || hasAlternatives))
            {
                try
                {
                    var fields = new[]
                    {
                        new Services.ElicitationCompat.Field(
                            Name: "choice",
                            Kind: "string",
                            Description: hasAlternatives ?
                                $"Pick a flow ({string.Join(", ", recommendation.AlternativeFlows!.Select(a => a.Name))}) or leave blank" :
                                "Optionally name a flow you intended",
                            Options: hasAlternatives ? recommendation.AlternativeFlows!.Select(a => a.Name).ToArray() : null
                        ),
                        new Services.ElicitationCompat.Field(
                            Name: "clarification",
                            Kind: "string",
                            Description: "Briefly clarify the use case (e.g., 'export vendors with UI links')"
                        )
                    };

                    var (accepted, content) = await Services.ElicitationCompat.TryElicitAsync(
                        server,
                        "Multiple plausible flows found or low confidence. Choose one or clarify your intent.",
                        fields,
                        ct);

                    if (accepted && content is not null)
                    {
                        var choice = content.TryGetValue("choice", out var choiceEl) &&
                                     choiceEl.ValueKind == System.Text.Json.JsonValueKind.String
                            ? choiceEl.GetString()
                            : null;
                        var clarification = content.TryGetValue("clarification", out var clarEl) &&
                                            clarEl.ValueKind == System.Text.Json.JsonValueKind.String
                            ? clarEl.GetString()
                            : null;

                        var refined = string.Join(" ", new[] { useCase, choice, clarification }
                            .Where(s => !string.IsNullOrWhiteSpace(s))!);

                        if (!string.Equals(refined, useCase, System.StringComparison.Ordinal))
                        {
                            recommendation = await recommender.RecommendAsync(refined, ct);
                            recommendation.Summary ??= $"Refined recommendation for ERP '{erp}'";
                            recommendation.NextActions ??= new List<ResourceLinkBlock>();
                        }
                    }
                }
                catch
                {
                    // Ignore elicitation failures and proceed with the initial recommendation
                }
            }
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
}
