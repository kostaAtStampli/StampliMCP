using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Tools;

internal static class KnowledgeValidationHelper
{
    internal static async Task<IReadOnlyList<KnowledgeValidationEntry>> BuildReportAsync(ErpRegistry registry, CancellationToken ct)
    {
        var results = new List<KnowledgeValidationEntry>();

        foreach (var descriptor in registry.ListErps())
        {
            using var facade = registry.GetFacade(descriptor.Key);
            var knowledge = facade.Knowledge;
            var flowService = facade.Flow;

            var issues = new List<string>();
            var warnings = new List<string>();
            var categorySummaries = new List<KnowledgeValidationCategory>();
            var operationSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var categories = await knowledge.GetCategoriesAsync(ct).ConfigureAwait(false);

            foreach (var category in categories)
            {
                var operations = await knowledge.GetOperationsByCategoryAsync(category.Name, ct).ConfigureAwait(false);
                if (category.Count >= 0 && category.Count != operations.Count)
                {
                    warnings.Add($"Category '{category.Name}' count mismatch: declared {category.Count}, found {operations.Count}.");
                }

                foreach (var operation in operations)
                {
                    if (!operationSet.Add(operation.Method))
                    {
                        issues.Add($"Duplicate operation method detected: {operation.Method}.");
                    }

                    if (!string.Equals(operation.Category, category.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Operation '{operation.Method}' declares category '{operation.Category}' but is stored under '{category.Name}'.");
                    }
                }

                categorySummaries.Add(new KnowledgeValidationCategory(
                    category.Name,
                    category.Count >= 0 ? category.Count : null,
                    operations.Count));
            }

            var flowSummaries = new List<KnowledgeValidationFlow>();
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
                        foreach (var opElement in usedArray.EnumerateArray())
                        {
                            var opName = opElement.GetString();
                            if (string.IsNullOrWhiteSpace(opName))
                            {
                                continue;
                            }

                            referencedOperations.Add(opName);
                            var operation = await knowledge.GetOperationByMethodAsync(opName, ct).ConfigureAwait(false);
                            if (operation is null)
                            {
                                warnings.Add($"Flow '{flowName}' references unknown operation '{opName}'.");
                            }
                        }
                    }
                    else
                    {
                        warnings.Add($"Flow '{flowName}' does not list 'usedByOperations'.");
                    }

                    flowSummaries.Add(new KnowledgeValidationFlow(flowName, referencedOperations));
                }
            }

            results.Add(new KnowledgeValidationEntry(
                descriptor.Key,
                issues.Count == 0,
                issues,
                warnings,
                new KnowledgeValidationSummary(categories.Count, operationSet.Count, flowSummaries.Count),
                categorySummaries,
                flowSummaries));
        }

        return results;
    }
}

internal sealed record KnowledgeValidationEntry(
    string Erp,
    bool Success,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Warnings,
    KnowledgeValidationSummary Summary,
    IReadOnlyList<KnowledgeValidationCategory> Categories,
    IReadOnlyList<KnowledgeValidationFlow> Flows);

internal sealed record KnowledgeValidationSummary(int Categories, int Operations, int Flows);

internal sealed record KnowledgeValidationCategory(string Name, int? Expected, int Actual);

internal sealed record KnowledgeValidationFlow(string Name, IReadOnlyList<string> Operations);
