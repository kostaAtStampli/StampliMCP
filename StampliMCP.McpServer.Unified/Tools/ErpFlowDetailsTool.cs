using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Tools;

internal static class FlowDetailsBuilder
{
    internal static async Task<FlowDetail?> BuildAsync(string erp, string flowName, FlowServiceBase flowService, CancellationToken ct)
    {
        var document = await flowService.GetFlowAsync(flowName, ct);
        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        var detail = new FlowDetail
        {
            Name = flowName,
            Description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
            Anatomy = new FlowAnatomy(),
            Constants = new Dictionary<string, ConstantInfo>(),
            ValidationRules = new List<string>(),
            CodeSnippets = new Dictionary<string, string>(),
            CriticalFiles = new List<FileReference>(),
            NextActions = new List<ResourceLinkBlock>
            {
                new()
                {
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query={Uri.EscapeDataString(flowName)}&scope=flows",
                    Name = "Search flow knowledge"
                },
                new()
                {
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=*&scope=operations",
                    Name = "Browse operations"
                }
            }
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

        if (root.TryGetProperty("usedByOperations", out var usedByNode))
        {
            var operations = usedByNode.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)
                .ToList();

            detail.NextActions.Insert(0, new ResourceLinkBlock
            {
                Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query={Uri.EscapeDataString(flowName)}&scope=operations",
                Name = $"Operations using {flowName}",
                Description = string.Join(", ", operations)
            });
        }

        detail.Summary = $"{flowName}: constants={detail.Constants.Count}, validationRules={detail.ValidationRules.Count}";

        return detail;
    }
}
