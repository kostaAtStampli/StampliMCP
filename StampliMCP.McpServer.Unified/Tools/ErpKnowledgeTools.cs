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
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpKnowledgeTools
{
    [McpServerTool(
        Name = "erp__query_knowledge",
        Title = "ERP Knowledge Search",
        UseStructuredContent = true)]
    [Description("Search ERP operations and flows using natural language.")]
    public static async Task<CallToolResult> QueryKnowledge(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Query text (operation name, entity, question)")] string query,
        [Description("Optional scope filter: operations, flows, constants, or all")] string? scope,
        IMcpServer server,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);

        var knowledge = facade.Knowledge;
        var flowService = facade.Flow;
        var fuzzyMatcher = facade.GetService<FuzzyMatchingService>();

        // Normalize scope to be case-insensitive across all branches
        var normalizedScope = scope?.Trim().ToLowerInvariant();
        var validScopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "operations", "flows", "constants", "all" };

        // If scope is not provided or invalid, opportunistically elicit a scope from the user
        if (normalizedScope is null || !validScopes.Contains(normalizedScope))
        {
            var fields = new[]
            {
                new Services.ElicitationCompat.Field(
                    Name: "scope",
                    Kind: "string",
                    Description: "Choose scope: operations | flows | constants | all",
                    Options: new[] { "operations", "flows", "constants", "all" }
                )
            };

            var outcome = await Services.ElicitationCompat.TryElicitAsync(
                server,
                "Select a scope to narrow the knowledge search.",
                fields,
                ct);

            if (outcome.Supported)
            {
                Log.Debug("Elicitation for query scope: action={Action}", outcome.Action ?? "none");

                if (string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase) &&
                    outcome.Content is { } content &&
                    content.TryGetValue("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.String)
                {
                    var chosen = scopeElement.GetString();
                    if (!string.IsNullOrWhiteSpace(chosen))
                    {
                        normalizedScope = chosen!.Trim().ToLowerInvariant();
                    }
                }
            }
        }

        var structured = await BuildKnowledgeResult(erp, query, normalizedScope, knowledge, flowService, fuzzyMatcher, ct);

        // If results are too broad, offer a refinement prompt
        if ((structured.MatchedOperations?.Count ?? 0) > 20 || (structured.RelevantFlows?.Count ?? 0) > 10)
        {
            var fields = new[]
            {
                new Services.ElicitationCompat.Field(
                    Name: "refine",
                    Kind: "string",
                    Description: "Add keywords to refine (e.g., 'vendor export validation')"
                ),
                new Services.ElicitationCompat.Field(
                    Name: "scope",
                    Kind: "string",
                    Description: "Optionally narrow scope again: operations | flows | constants | all",
                    Options: new[] { "operations", "flows", "constants", "all" }
                )
            };

            var outcome = await Services.ElicitationCompat.TryElicitAsync(
                server,
                $"Found {structured.MatchedOperations.Count} operations and {structured.RelevantFlows.Count} flows. Refine your search?",
                fields,
                ct);

            if (outcome.Supported)
            {
                Log.Debug("Elicitation for query refinement: action={Action}", outcome.Action ?? "none");

                if (string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase) &&
                    outcome.Content is { } content)
                {
                    var refine = content.TryGetValue("refine", out var refElement) && refElement.ValueKind == JsonValueKind.String ? refElement.GetString() : null;
                    var scope2 = content.TryGetValue("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.String ? scopeElement.GetString() : null;
                    var newScope = string.IsNullOrWhiteSpace(scope2) ? normalizedScope : scope2!.Trim().ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(refine) || !string.Equals(newScope, normalizedScope, StringComparison.Ordinal))
                    {
                        normalizedScope = newScope;
                        var refinedQuery = string.IsNullOrWhiteSpace(refine) ? query : refine!;
                        structured = await BuildKnowledgeResult(erp, refinedQuery, normalizedScope, knowledge, flowService, fuzzyMatcher, ct);
                    }
                }
            }
        }

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

        var instructionText = ToolLinkFormatter.BuildInstructionList(structured.NextActions);
        if (!string.IsNullOrWhiteSpace(instructionText))
        {
            result.Content.Add(new TextContentBlock
            {
                Type = "text",
                Text = instructionText
            });
        }

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

                if (root.TryGetProperty("constants", out var constObj) && (scope is null or "all" or "constants"))
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

                if (root.TryGetProperty("validationRules", out var rules) && (scope is null || scope is "all"))
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

                if (root.TryGetProperty("codeSnippets", out var snippetObj) && (scope is null || scope is "all"))
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
                Uri = $"mcp://stampli-unified/erp/{erp}/flows",
                Name = $"{erp} flow catalog",
                Description = "Open flow guidance via resources/read."
            },
            new()
            {
                Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=*&scope=operations",
                Name = "Browse operations"
            },
            new()
            {
                Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=*&scope=flows",
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
