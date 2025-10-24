using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpFlowDetailsTool
{
    [McpServerTool(
        Name = "erp__get_flow_details",
        Title = "ERP Flow Details",
        UseStructuredContent = true)]
    [Description("Get complete details for a flow within a specific ERP module.")]
    public static async Task<CallToolResult> GetFlowDetails(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Flow name as defined in the module's flow catalog") ] string flow,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);
        var flowService = facade.Flow ?? throw new InvalidOperationException($"ERP '{erp}' does not expose flow metadata.");

        var doc = await flowService.GetFlowAsync(flow, ct);
        if (doc is null)
        {
            var notFound = new FlowDetail
            {
                Name = flow,
                Description = $"Flow '{flow}' not found",
                NextActions = new List<ResourceLinkBlock>
                {
                    new()
                    {
                        Uri = $"mcp://stampli-unified/erp__list_flows?erp={erp}",
                        Name = "List available flows"
                    }
                }
            };

            var nfResult = new CallToolResult
            {
                StructuredContent = JsonSerializer.SerializeToNode(new { result = notFound })
            };

            nfResult.Content.Add(new TextContentBlock
            {
                Type = "text",
                Text = JsonSerializer.Serialize(notFound, new JsonSerializerOptions { WriteIndented = true })
            });

            return nfResult;
        }

        var root = doc.RootElement;
        var detail = new FlowDetail
        {
            Name = flow,
            Description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
            NextActions = new List<ResourceLinkBlock>
            {
                new()
                {
                    Uri = $"mcp://stampli-unified/erp__list_operations?erp={erp}",
                    Name = "Review operations"
                },
                new()
                {
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query={Uri.EscapeDataString(flow)}",
                    Name = "Search related knowledge"
                }
            }
        };

        if (root.TryGetProperty("anatomy", out var anatomyNode))
        {
            var anatomy = new FlowAnatomy
            {
                Flow = anatomyNode.TryGetProperty("flow", out var flowStep) ? flowStep.GetString() ?? string.Empty : string.Empty,
                Validation = anatomyNode.TryGetProperty("validation", out var validation) ? validation.GetString() : null,
                Mapping = anatomyNode.TryGetProperty("mapping", out var mapping) ? mapping.GetString() : null,
            };

            var additional = anatomyNode.EnumerateObject()
                .Where(p => p.Name is not "flow" and not "validation" and not "mapping")
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);

            anatomy.AdditionalInfo = additional;
            detail.Anatomy = anatomy;
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

        if (root.TryGetProperty("usedByOperations", out var usedByNode))
        {
            detail.NextActions.Insert(0, new ResourceLinkBlock
            {
                Uri = $"mcp://stampli-unified/erp__list_operations?erp={erp}",
                Name = $"Operations using {flow}",
                Description = string.Join(", ", usedByNode.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!))
            });
        }

        detail.Summary = $"{flow}: constants={detail.Constants.Count}, validationRules={detail.ValidationRules.Count}";

        var callResult = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = detail })
        };

        callResult.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = JsonSerializer.Serialize(detail, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            })
        });

        foreach (var link in detail.NextActions)
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }
}
