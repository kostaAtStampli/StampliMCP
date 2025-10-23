using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpKnowledgeTools
{
    [McpServerTool(
        Name = "erp__list_operations",
        Title = "List ERP Operations",
        UseStructuredContent = true)]
    [Description("List operations for the given ERP with optional flow information.")]
    public static async Task<CallToolResult> ListOperations(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);

        var knowledge = facade.Knowledge;
        var flowService = facade.Flow;

        var summaries = new List<object>();
        var categories = await knowledge.GetCategoriesAsync(ct);

        foreach (var category in categories)
        {
            var operations = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            foreach (var operation in operations)
            {
                string? flowName = null;
                if (flowService is not null)
                {
                    flowName = await flowService.GetFlowForOperationAsync(operation.Method, ct);
                }

                summaries.Add(new
                {
                    method = operation.Method,
                    summary = operation.Summary,
                    category = category.Name,
                    flow = flowName
                });
            }
        }

        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = summaries })
        };

        result.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = JsonSerializer.Serialize(summaries, new JsonSerializerOptions
            {
                WriteIndented = true
            })
        });

        return result;
    }

    [McpServerTool(
        Name = "erp__list_flows",
        Title = "List ERP Flows",
        UseStructuredContent = true)]
    [Description("List integration flows for the given ERP.")]
    public static async Task<CallToolResult> ListFlows(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);
        var flowService = facade.Flow;

        if (flowService is null)
        {
            throw new InvalidOperationException($"ERP '{erp}' does not expose flow metadata.");
        }

        var flows = new List<FlowSummary>();
        var names = await flowService.GetAllFlowNamesAsync(ct);

        foreach (var name in names)
        {
            var doc = await flowService.GetFlowAsync(name, ct);
            if (doc is null)
            {
                continue;
            }

            var root = doc.RootElement;
            var description = root.TryGetProperty("description", out var desc)
                ? desc.GetString()
                : null;

            var usedBy = new List<string>();
            if (root.TryGetProperty("usedByOperations", out var usedArray))
            {
                usedBy.AddRange(usedArray.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))!);
            }

            flows.Add(new FlowSummary
            {
                Name = name,
                Description = description ?? string.Empty,
                UsedByOperations = usedBy
            });
        }

        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = flows })
        };

        result.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = JsonSerializer.Serialize(flows, new JsonSerializerOptions
            {
                WriteIndented = true
            })
        });

        return result;
    }

    [McpServerTool(
        Name = "erp__query_knowledge",
        Title = "ERP Knowledge Search",
        UseStructuredContent = true)]
    [Description("Search ERP operations and flows using natural language.")]
    public static async Task<CallToolResult> QueryKnowledge(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Query text (operation name, entity, question)")] string query,
        [Description("Optional scope filter: operations, flows, constants, or all")] string? scope,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);

        var knowledge = facade.Knowledge;
        var flowService = facade.Flow;
        var fuzzyMatcher = facade.GetService<FuzzyMatchingService>();

        var structured = await BuildKnowledgeResult(erp, query, scope, knowledge, flowService, fuzzyMatcher, ct);

        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = structured })
        };

        var json = JsonSerializer.Serialize(structured, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        result.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = json
        });

        foreach (var link in structured.NextActions)
        {
            result.Content.Add(link);
        }

        return result;
    }

    private static async Task<KnowledgeQueryResult> BuildKnowledgeResult(
        string erp,
        string query,
        string? scope,
        KnowledgeServiceBase knowledge,
        FlowServiceBase? flowService,
        FuzzyMatchingService? fuzzyMatcher,
        CancellationToken ct)
    {
        var tokens = Tokenize(query);
        var categories = await knowledge.GetCategoriesAsync(ct);
        var operations = new List<OperationSummary>();

        foreach (var category in categories)
        {
            var ops = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            foreach (var op in ops)
            {
                if (scope is null or "all" or "operations")
                {
                    if (tokens.Length == 0 || Matches(op.Method, op.Summary, category.Name, tokens, fuzzyMatcher))
                    {
                        operations.Add(new OperationSummary
                        {
                            Method = op.Method,
                            Summary = op.Summary,
                            Category = category.Name
                        });
                    }
                }
            }
        }

        var flowSummaries = new List<FlowSummary>();
        var constants = new Dictionary<string, ConstantInfo>();
        var validationRules = new List<string>();
        var snippets = new List<CodeSnippet>();

        if (flowService is not null && (scope is null or "all" or "flows" or "constants"))
        {
            var flowNames = await flowService.GetAllFlowNamesAsync(ct);
            foreach (var flowName in flowNames)
            {
                var doc = await flowService.GetFlowAsync(flowName, ct);
                if (doc is null)
                {
                    continue;
                }

                var root = doc.RootElement;
                var description = root.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? string.Empty
                    : string.Empty;

                if (tokens.Length == 0 || Matches(flowName, description, flowName, tokens, fuzzyMatcher))
                {
                    var summary = new FlowSummary
                    {
                        Name = flowName,
                        Description = description
                    };

                    if (root.TryGetProperty("usedByOperations", out var used))
                    {
                        summary.UsedByOperations = used.EnumerateArray()
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s!)
                            .ToList();
                    }

                    flowSummaries.Add(summary);
                }

                if (root.TryGetProperty("constants", out var constObj) && (scope is null or "all" || scope.Equals("constants", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var constant in constObj.EnumerateObject())
                    {
                        constants[constant.Name] = new ConstantInfo
                        {
                            Name = constant.Name,
                            Value = constant.Value.TryGetProperty("value", out var valueEl) ? valueEl.ToString() ?? string.Empty : string.Empty,
                            File = constant.Value.TryGetProperty("file", out var fileEl) ? fileEl.GetString() : null,
                            Line = constant.Value.TryGetProperty("line", out var lineEl) && lineEl.TryGetInt32(out var line) ? line : null,
                            Purpose = constant.Value.TryGetProperty("purpose", out var purposeEl) ? purposeEl.GetString() : null
                        };
                    }
                }

                if (root.TryGetProperty("validationRules", out var rules) && (scope is null || scope.Equals("all", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var rule in rules.EnumerateArray())
                    {
                        var text = rule.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            validationRules.Add(text);
                        }
                    }
                }

                if (root.TryGetProperty("codeSnippets", out var snippetObj) && (scope is null || scope.Equals("all", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var snippet in snippetObj.EnumerateObject())
                    {
                        snippets.Add(new CodeSnippet
                        {
                            Name = snippet.Name,
                            Code = snippet.Value.GetString() ?? string.Empty,
                            Language = "text",
                            Explanation = flowName
                        });
                    }
                }
            }
        }

        var nextActions = new List<ResourceLinkBlock>
        {
            new()
            {
                Uri = $"mcp://stampli-unified/erp__list_operations?erp={erp}",
                Name = "Browse operations"
            },
            new()
            {
                Uri = $"mcp://stampli-unified/erp__list_flows?erp={erp}",
                Name = "Browse flows"
            }
        };

        return new KnowledgeQueryResult
        {
            Summary = $"Found {operations.Count} operations and {flowSummaries.Count} flows",
            MatchedOperations = operations,
            RelevantFlows = flowSummaries,
            Constants = constants,
            ValidationRules = validationRules,
            CodeExamples = snippets,
            NextActions = nextActions
        };
    }

    private static string[] Tokenize(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim() == "*")
        {
            return Array.Empty<string>();
        }

        return query.ToLowerInvariant()
            .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Distinct()
            .ToArray();
    }

    private static bool Matches(string method, string summary, string category, string[] tokens, FuzzyMatchingService? fuzzy)
    {
        if (tokens.Length == 0)
        {
            return true;
        }

        var text = $"{method} {summary} {category}".ToLowerInvariant();

        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fuzzy is not null)
            {
                var match = fuzzy.FindBestMatch(token, text.Split(' ', StringSplitOptions.RemoveEmptyEntries), fuzzy.GetThreshold("keyword"));
                if (match is not null)
                {
                    continue;
                }
            }

            return false;
        }

        return true;
    }
}
