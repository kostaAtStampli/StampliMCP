using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using System;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class AcumaticaRecommendationService : IErpRecommendationService
{
    private readonly ILogger<AcumaticaRecommendationService> _logger;
    private readonly AcumaticaFlowService _flowService;

    public AcumaticaRecommendationService(
        ILogger<AcumaticaRecommendationService> logger,
        AcumaticaFlowService flowService)
    {
        _logger = logger;
        _flowService = flowService;
    }

    public async Task<FlowRecommendation> RecommendAsync(string useCase, CancellationToken ct)
    {
        var description = useCase ?? string.Empty;
        var analysis = _flowService.MatchFeatureToFlow(description, ct);
        var primary = analysis.Primary;

        var detail = await BuildFlowDetailAsync(primary.FlowName, ct);

        var recommendation = new FlowRecommendation
        {
            FlowName = primary.FlowName,
            Confidence = Math.Clamp(primary.OverallScore, 0.0, 1.0),
            Reasoning = primary.Reasoning,
            Details = detail,
            Summary = $"Recommended flow '{primary.FlowName}' ({primary.ConfidenceLabel})",
            AlternativeFlows = analysis.Alternatives
                .Select(a => new AlternativeFlow
                {
                    Name = a.FlowName,
                    Confidence = Math.Clamp(a.OverallScore, 0.0, 1.0),
                    Reason = a.Reasoning
                })
                .ToList(),
            Scores = new Dictionary<string, double>
            {
                ["overall"] = Math.Clamp(primary.OverallScore, 0.0, 1.0),
                ["entity"] = Math.Clamp(primary.EntityScore, 0.0, 1.0),
                ["action"] = Math.Clamp(primary.ActionScore, 0.0, 1.0),
                ["keywords"] = Math.Clamp(primary.KeywordScore, 0.0, 1.0)
            },
            NextActions = new List<ResourceLinkBlock>
            {
                new()
                {
                    Uri = "mcp://stampli-unified/erp__list_flows?erp=acumatica",
                    Name = "Browse all flows"
                },
                new()
                {
                    Uri = $"mcp://stampli-unified/erp__get_flow_details?erp=acumatica&flow={Uri.EscapeDataString(primary.FlowName)}",
                    Name = $"View {primary.FlowName} details"
                }
            }
        };

        return recommendation;
    }

    private async Task<FlowDetail> BuildFlowDetailAsync(string flowName, CancellationToken ct)
    {
        var doc = await _flowService.GetFlowAsync(flowName, ct);
        if (doc is null)
        {
            _logger.LogWarning("Flow metadata not found for {Flow}", flowName);
            return new FlowDetail
            {
                Name = flowName,
                Description = "Flow metadata not found",
                NextActions = new List<ResourceLinkBlock>()
            };
        }

        var root = doc.RootElement;

        var detail = new FlowDetail
        {
            Name = flowName,
            Description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
            Anatomy = new FlowAnatomy(),
            Constants = new Dictionary<string, ConstantInfo>(),
            ValidationRules = new List<string>(),
            CodeSnippets = new Dictionary<string, string>(),
            CriticalFiles = new List<FileReference>(),
            NextActions = new List<ResourceLinkBlock>()
        };

        if (root.TryGetProperty("anatomy", out var anatomyNode))
        {
            detail.Anatomy.Flow = anatomyNode.TryGetProperty("flow", out var flowStep) ? flowStep.GetString() ?? string.Empty : string.Empty;
            detail.Anatomy.Validation = anatomyNode.TryGetProperty("validation", out var validation) ? validation.GetString() : null;
            detail.Anatomy.Mapping = anatomyNode.TryGetProperty("mapping", out var mapping) ? mapping.GetString() : null;
            detail.Anatomy.AdditionalInfo = anatomyNode.EnumerateObject()
                .Where(p => p.Name is not "flow" and not "validation" and not "mapping")
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
        }

        if (root.TryGetProperty("constants", out var constantsNode))
        {
            foreach (var constant in constantsNode.EnumerateObject())
            {
                detail.Constants[constant.Name] = new ConstantInfo
                {
                    Name = constant.Name,
                    Value = constant.Value.TryGetProperty("value", out var value) ? value.ToString() ?? string.Empty : string.Empty,
                    File = constant.Value.TryGetProperty("file", out var file) ? file.GetString() : null,
                    Line = constant.Value.TryGetProperty("line", out var line) && line.TryGetInt32(out var lineNumber) ? lineNumber : null,
                    Purpose = constant.Value.TryGetProperty("purpose", out var purpose) ? purpose.GetString() : null
                };
            }
        }

        if (root.TryGetProperty("validationRules", out var rulesNode))
        {
            detail.ValidationRules = rulesNode.EnumerateArray()
                .Select(r => r.GetString())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r!)
                .ToList();
        }

        if (root.TryGetProperty("codeSnippets", out var snippetsNode))
        {
            foreach (var snippet in snippetsNode.EnumerateObject())
            {
                detail.CodeSnippets[snippet.Name] = snippet.Value.GetString() ?? string.Empty;
            }
        }

        if (root.TryGetProperty("criticalFiles", out var filesNode))
        {
            foreach (var fileElement in filesNode.EnumerateArray())
            {
                var file = new FileReference
                {
                    File = fileElement.TryGetProperty("file", out var fileProp) ? fileProp.GetString() ?? string.Empty : string.Empty,
                    Lines = fileElement.TryGetProperty("lines", out var linesProp) ? linesProp.GetString() : null,
                    Purpose = fileElement.TryGetProperty("purpose", out var purposeProp) ? purposeProp.GetString() : null,
                    KeyPatterns = fileElement.TryGetProperty("keyPatterns", out var kpProp)
                        ? kpProp.EnumerateArray().Select(v => v.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList()
                        : new List<string>()
                };
                detail.CriticalFiles.Add(file);
            }
        }

        detail.Summary = $"{flowName}: constants={detail.Constants.Count}, validationRules={detail.ValidationRules.Count}";
        return detail;
    }

}
