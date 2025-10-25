using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using StampliMCP.McpServer.Unified.Tools;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Services;

/// <summary>
/// Provides MCP resource listings/responses for unified flows and related entities.
/// Keeps resource URIs stable so clients can preview flow guidance without invoking tools.
/// </summary>
internal sealed class UnifiedResourceCatalog
{
    private const string BaseUri = "mcp://stampli-unified";

    private readonly ErpRegistry _registry;
    private readonly ILogger<UnifiedResourceCatalog> _logger;

    public UnifiedResourceCatalog(
        ErpRegistry registry,
        ILogger<UnifiedResourceCatalog> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async ValueTask<ListResourcesResult> ListResourcesAsync(CancellationToken ct)
    {
        var resources = new List<Resource>();

        foreach (var descriptor in _registry.ListErps())
        {
            var erp = descriptor.Key;
            resources.Add(new Resource
            {
                Uri = $"{BaseUri}/erp/{erp}/flows",
                Name = $"{erp} flows",
                Description = $"Browse flow catalog for {erp}"
            });

            using var facade = _registry.GetFacade(erp);
            var flowService = facade.Flow;
            if (flowService is null)
            {
                continue;
            }

            var flowNames = await flowService
                .GetAllFlowNamesAsync(ct)
                .ConfigureAwait(false);

            foreach (var flow in flowNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                resources.Add(new Resource
                {
                    Uri = $"{BaseUri}/erp/{erp}/flows/{flow}",
                    Name = $"{erp}:{flow}",
                    Description = $"Flow guidance for {flow} ({erp})"
                });
            }
        }

        return new ListResourcesResult { Resources = resources };
    }

    public ValueTask<ReadResourceResult> ReadResourceAsync(string? uri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new McpProtocolException("Resource URI is required.", McpErrorCode.InvalidParams);
        }

        if (!uri.StartsWith(BaseUri, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(BuildToolLinkResponse(uri));
        }

        var remainder = uri.Length > BaseUri.Length
            ? uri[(BaseUri.Length + 1)..]
            : string.Empty;

        if (string.IsNullOrEmpty(remainder))
        {
            return ValueTask.FromResult(BuildToolLinkResponse(uri));
        }

        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 3 && string.Equals(segments[0], "erp", StringComparison.OrdinalIgnoreCase))
        {
            var erp = segments[1];

            if (!_registry.TryGetDescriptor(erp, out var descriptor) || descriptor is null)
            {
                throw new McpProtocolException($"Unknown ERP: {erp}", McpErrorCode.InvalidParams);
            }

            if (segments.Length == 3 && string.Equals(segments[2], "flows", StringComparison.OrdinalIgnoreCase))
            {
                return BuildFlowIndexAsync(descriptor.Key, ct);
            }

            if (segments.Length == 4 && string.Equals(segments[2], "flows", StringComparison.OrdinalIgnoreCase))
            {
                var flowName = segments[3];
                return BuildFlowDetailAsync(descriptor.Key, flowName, ct);
            }
        }

        return ValueTask.FromResult(BuildToolLinkResponse(uri));
    }

    private async ValueTask<ReadResourceResult> BuildFlowIndexAsync(string erp, CancellationToken ct)
    {
        using var facade = _registry.GetFacade(erp);
        var flowService = facade.Flow;
        if (flowService is null)
        {
            throw new McpProtocolException($"ERP '{erp}' does not expose flows.", McpErrorCode.InvalidParams);
        }

        var flowNames = await flowService.GetAllFlowNamesAsync(ct).ConfigureAwait(false);
        var ordered = flowNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

        var payload = new
        {
            erp,
            flows = ordered.Select(name => new
            {
                name,
                uri = $"{BaseUri}/erp/{erp}/flows/{name}"
            })
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        var markdown = new StringBuilder()
            .AppendLine($"# {erp} flow catalog")
            .AppendLine()
            .AppendJoin(Environment.NewLine, ordered.Select(name => $"- {name}"))
            .ToString();

        return new ReadResourceResult
        {
            Contents =
            {
                new TextResourceContents { MimeType = "application/json", Text = json },
                new TextResourceContents { MimeType = "text/markdown", Text = markdown }
            }
        };
    }

    private async ValueTask<ReadResourceResult> BuildFlowDetailAsync(string erp, string flowName, CancellationToken ct)
    {
        using var facade = _registry.GetFacade(erp);
        var flowService = facade.Flow;
        if (flowService is null)
        {
            throw new McpProtocolException($"ERP '{erp}' does not expose flows.", McpErrorCode.InvalidParams);
        }

        var detail = await FlowDetailsBuilder.BuildAsync(erp, flowName, flowService, ct).ConfigureAwait(false);
        if (detail is null)
        {
            _logger.LogWarning("Flow {FlowName} not found for ERP {Erp}", flowName, erp);
            throw new McpProtocolException($"Flow '{flowName}' not found for ERP '{erp}'.", McpErrorCode.InvalidParams);
        }

        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var markdown = BuildFlowMarkdown(detail);

        return new ReadResourceResult
        {
            Contents =
            {
                new TextResourceContents { MimeType = "application/json", Text = json },
                new TextResourceContents { MimeType = "text/markdown", Text = markdown }
            }
        };
    }

    private static string BuildFlowMarkdown(FlowDetail detail)
    {
        var sb = new StringBuilder().AppendLine($"# {detail.Name}");

        if (!string.IsNullOrWhiteSpace(detail.Description))
        {
            sb.AppendLine().AppendLine(detail.Description);
        }

        if (!string.IsNullOrWhiteSpace(detail.Anatomy?.Flow))
        {
            sb.AppendLine().AppendLine($"**Anatomy:** {detail.Anatomy.Flow}");
        }

        if (detail.ValidationRules?.Count > 0)
        {
            sb.AppendLine().AppendLine("**Validation Rules:**");
            foreach (var rule in detail.ValidationRules)
            {
                sb.AppendLine($"- {rule}");
            }
        }

        if (detail.Constants?.Count > 0)
        {
            sb.AppendLine().AppendLine("**Constants:**");
            foreach (var constant in detail.Constants.Values)
            {
                if (constant is null)
                {
                    continue;
                }

                sb.AppendLine($"- {constant.Name}: {constant.Value}");
            }
        }

        if (detail.CriticalFiles?.Count > 0)
        {
            sb.AppendLine().AppendLine("**Critical Files:**");
            foreach (var file in detail.CriticalFiles)
            {
                var purpose = string.IsNullOrWhiteSpace(file.Purpose) ? string.Empty : $" — {file.Purpose}";
                sb.AppendLine($"- {file.File}{purpose}");
            }
        }

        return sb.ToString();
    }

    private static ReadResourceResult BuildToolLinkResponse(string uri)
    {
        var example = new
        {
            method = "tools/call",
            name = NormalizeToolName(ExtractToolName(uri)),
            arguments = ExtractArguments(uri)
        };

        var payload = new
        {
            note = "This is a tool link. Use tools/call to execute.",
            example
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

        return new ReadResourceResult
        {
            Contents =
            {
                new TextResourceContents { MimeType = "application/json", Text = json }
            }
        };
    }

    private static string ExtractToolName(string uri)
    {
        try
        {
            var parsed = new Uri(uri.Replace("mcp://", "http://"), UriKind.Absolute);
            return parsed.AbsolutePath.Trim('/');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IDictionary<string, object?> ExtractArguments(string uri)
    {
        try
        {
            var parsed = new Uri(uri.Replace("mcp://", "http://"), UriKind.Absolute);
            var query = QueryHelpers.ParseQuery(parsed.Query);
            return query.ToDictionary(pair => pair.Key, pair => (object?)pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string NormalizeToolName(string raw)
    {
        return raw switch
        {
            "erpquery_knowledge" => "erp__query_knowledge",
            "erprecommend_flow" => "erp__recommend_flow",
            "erpvalidate_request" => "erp__validate_request",
            "erpdiagnose_error" => "erp__diagnose_error",
            "erphealth_check" => "erp__health_check",
            "erpknowledge_update_plan" => "erp__knowledge_update_plan",
            _ => raw
        };
    }
}
