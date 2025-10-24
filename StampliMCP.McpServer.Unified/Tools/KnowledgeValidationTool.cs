using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Services;
using System.Threading;
using System.Threading.Tasks;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class KnowledgeValidationTool
{
    [McpServerTool(
        Name = "mcp__validate_embedded_knowledge",
        Title = "Validate Embedded Knowledge",
        UseStructuredContent = true)]
    [Description("Validate embedded categories, operations, and flows across registered ERPs.")]
    public static async Task<CallToolResult> Execute(ErpRegistry registry, CancellationToken ct)
    {
        var results = new List<object>();

        foreach (var descriptor in registry.ListErps())
        {
            using var facade = registry.GetFacade(descriptor.Key);
            var knowledge = facade.Knowledge;
            var flowService = facade.Flow;

            var issues = new List<string>();
            var warnings = new List<string>();
            var categorySummaries = new List<object>();
            var operationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var categories = await knowledge.GetCategoriesAsync(ct).ConfigureAwait(false);

            foreach (var category in categories)
            {
                var operations = await knowledge.GetOperationsByCategoryAsync(category.Name, ct).ConfigureAwait(false);
                if (category.Count >= 0 && category.Count != operations.Count)
                {
                    warnings.Add($"Category '{category.Name}' count mismatch: declared {category.Count}, found {operations.Count}.");
                }

                foreach (var op in operations)
                {
                    if (!operationSet.Add(op.Method))
                    {
                        issues.Add($"Duplicate operation method detected: {op.Method}.");
                    }

                    if (!string.Equals(op.Category, category.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Operation '{op.Method}' declares category '{op.Category}' but is stored under '{category.Name}'.");
                    }
                }

                categorySummaries.Add(new
                {
                    name = category.Name,
                    expected = category.Count,
                    actual = operations.Count
                });
            }

            var flowSummaries = new List<object>();
            if (flowService is not null)
            {
                var flowNames = await flowService.GetAllFlowNamesAsync(ct).ConfigureAwait(false);
                foreach (var flowName in flowNames)
                {
                    var doc = await flowService.GetFlowAsync(flowName, ct).ConfigureAwait(false);
                    if (doc is null)
                    {
                        issues.Add($"Flow '{flowName}' could not be loaded.");
                        continue;
                    }

                    var root = doc.RootElement;
                    if (!root.TryGetProperty("description", out var descEl) || string.IsNullOrWhiteSpace(descEl.GetString()))
                    {
                        warnings.Add($"Flow '{flowName}' is missing a description.");
                    }

                    var referencedOperations = new List<string>();
                    if (root.TryGetProperty("usedByOperations", out var usedArray))
                    {
                        foreach (var opEl in usedArray.EnumerateArray())
                        {
                            var opName = opEl.GetString();
                            if (string.IsNullOrWhiteSpace(opName))
                            {
                                continue;
                            }

                            referencedOperations.Add(opName);
                            var op = await knowledge.GetOperationByMethodAsync(opName, ct).ConfigureAwait(false);
                            if (op is null)
                            {
                                warnings.Add($"Flow '{flowName}' references unknown operation '{opName}'.");
                            }
                        }
                    }
                    else
                    {
                        warnings.Add($"Flow '{flowName}' does not list 'usedByOperations'.");
                    }

                    flowSummaries.Add(new
                    {
                        name = flowName,
                        operations = referencedOperations
                    });
                }
            }

            results.Add(new
            {
                erp = descriptor.Key,
                success = issues.Count == 0,
                issues,
                warnings,
                summary = new
                {
                    categories = categories.Count,
                    operations = operationSet.Count,
                    flows = flowSummaries.Count
                },
                categories = categorySummaries,
                flows = flowSummaries
            });
        }

        var payload = new { result = results };

        var callResult = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(payload)
        };

        callResult.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            })
        });

        return callResult;
    }
}
