using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class KnowledgeQueryTool
{
    // Elicitation input - only primitive/enum types supported
    public sealed class RefinementInput
    {
        public string Type { get; set; } = "all";
        public string? Refinement { get; set; }
    }

    [McpServerTool(
        Name = "query_acumatica_knowledge",
        Title = "Acumatica Knowledge Explorer",
        UseStructuredContent = true
    )]
    [Description(@"
Search 48 Acumatica operations and 9 integration flows with natural language.
Uses interactive elicitation to refine ambiguous queries (Protocol 2025-06-18).

Examples:
- ""vendor operations"" → Returns VENDOR_EXPORT_FLOW + 4 vendor operations
- ""pagination limits"" → Returns STANDARD_IMPORT_FLOW with 2000 row/page constant
- ""payment export"" → Returns PAYMENT_FLOW with international payment logic
- ""how to handle errors"" → Returns error patterns from flows

Features:
✓ Smart search across operations, flows, constants, and code snippets
✓ Interactive refinement when results are ambiguous (>10 ops or >3 flows)
✓ Structured JSON output with typed schemas
✓ Resource links to related tools (TDD workflow, flow details, etc.)

Returns:
• Matching operations (method, summary, category)
• Relevant flows (anatomy, constants, validation rules)
• Code snippets from real implementations
• Next action resource links
")]
    public static async Task<KnowledgeQueryResult> Execute(
        [Description("Query: operation name, entity type, pattern, or natural language question")]
        string query,

        [Description("Optional scope filter: operations, flows, constants, or all")]
        string? scope,

        ModelContextProtocol.Server.McpServer server, // DI injects McpServer for ElicitAsync<T>
        KnowledgeService knowledge,
        FlowService flowService,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: query={Query}, scope={Scope}",
            "query_acumatica_knowledge", query, scope ?? "all");

        try
        {
            // Step 1: Search knowledge base
            var searchResults = await SearchKnowledge(query, scope, knowledge, flowService, ct);

            // Step 2: Elicit clarification if ambiguous (Protocol 2025-06-18 - generic API)
            if (searchResults.IsAmbiguous)
            {
                var message = searchResults.Operations.Any() && searchResults.Flows.Any()
                    ? $"Found {searchResults.TotalMatches} matches ({searchResults.Operations.Count} operations, {searchResults.Flows.Count} flows). Refine search:"
                    : searchResults.Operations.Any()
                    ? $"Found {searchResults.Operations.Count} operations. Provide more specific terms:"
                    : $"Found {searchResults.Flows.Count} flows. Provide more specific terms:";

                var elicitResult = await server.ElicitAsync<RefinementInput>(message, cancellationToken: ct);

                if (elicitResult.Action == "accept" && elicitResult.Content is { } refinement)
                {
                    var refinedScope = !string.IsNullOrEmpty(refinement.Type) && refinement.Type != "all"
                        ? refinement.Type
                        : scope;

                    var refinedQuery = !string.IsNullOrWhiteSpace(refinement.Refinement)
                        ? $"{query} {refinement.Refinement}"
                        : query;

                    searchResults = await SearchKnowledge(refinedQuery, refinedScope, knowledge, flowService, ct);
                }
            }

            // Step 3: Build structured result
            var result = new KnowledgeQueryResult
            {
                MatchedOperations = searchResults.Operations,
                RelevantFlows = searchResults.Flows,
                Constants = searchResults.Constants,
                CodeExamples = searchResults.CodeSnippets,
                ValidationRules = searchResults.ValidationRules,
                NextActions = BuildResourceLinks(searchResults)
            };

            Serilog.Log.Information("Tool {Tool} completed: ops={OpCount}, flows={FlowCount}",
                "query_acumatica_knowledge", result.MatchedOperations.Count, result.RelevantFlows.Count);

            return result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "query_acumatica_knowledge", ex.Message);

            return new KnowledgeQueryResult
            {
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = "mcp://stampli-acumatica/health_check",
                        Name = "Check server health"
                    }
                }
            };
        }
    }

    private static async Task<SearchResults> SearchKnowledge(
        string query,
        string? scope,
        KnowledgeService knowledge,
        FlowService flowService,
        CancellationToken ct)
    {
        var results = new SearchResults();
        var lowerQuery = query.ToLower();

        // Tokenize query for better matching (e.g., "vendor export" → ["vendor", "export"])
        var queryTokens = lowerQuery.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

        // Search operations
        if (scope == null || scope == "all" || scope == "operations")
        {
            var categories = await knowledge.GetCategoriesAsync(ct);
            foreach (var category in categories)
            {
                var ops = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
                var matchedOps = ops.Where(op =>
                {
                    var opMethodLower = op.Method.ToLower();
                    var opSummaryLower = op.Summary?.ToLower() ?? "";
                    var categoryLower = category.Name.ToLower();

                    // Match if ANY token appears in method, summary, or category
                    return queryTokens.Any(token =>
                        opMethodLower.Contains(token) ||
                        opSummaryLower.Contains(token) ||
                        categoryLower.Contains(token)
                    );
                }).ToList();

                results.Operations.AddRange(matchedOps.Select(op => new OperationSummary
                {
                    Method = op.Method,
                    Summary = op.Summary ?? "",
                    Category = category.Name,
                    Flow = null // Will be set when matching to flows
                }));
            }
        }

        // Search flows
        if (scope == null || scope == "all" || scope == "flows")
        {
            var flowNames = new[]
            {
                "VENDOR_EXPORT_FLOW", "PAYMENT_FLOW", "STANDARD_IMPORT_FLOW",
                "PO_MATCHING_FLOW", "PO_MATCHING_FULL_IMPORT_FLOW", "EXPORT_INVOICE_FLOW",
                "EXPORT_PO_FLOW", "M2M_IMPORT_FLOW", "API_ACTION_FLOW"
            };

            foreach (var flowName in flowNames)
            {
                try
                {
                    var flowDoc = await flowService.GetFlowAsync(flowName, ct);
                    if (flowDoc == null) continue;

                    var flow = flowDoc.RootElement;
                    var description = flow.GetProperty("description").GetString() ?? "";
                    var flowNameLower = flowName.ToLower();
                    var descriptionLower = description.ToLower();

                    // Match flow by name or description (using tokens)
                    bool matches = queryTokens.Any(token =>
                        flowNameLower.Contains(token) ||
                        descriptionLower.Contains(token)
                    );

                    if (matches)
                    {
                        results.Flows.Add(new FlowSummary
                        {
                            Name = flowName,
                            Description = description,
                            UsedByOperations = flow.GetProperty("usedByOperations")
                                .EnumerateArray()
                                .Select(e => e.GetString() ?? "")
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList()
                        });

                        // Extract constants
                        if (flow.TryGetProperty("constants", out var constants))
                        {
                            foreach (var constant in constants.EnumerateObject())
                            {
                                var constObj = constant.Value;
                                results.Constants[constant.Name] = new ConstantInfo
                                {
                                    Name = constant.Name,
                                    Value = constObj.TryGetProperty("value", out var val) ? val.ToString() : "",
                                    File = constObj.TryGetProperty("file", out var file) ? file.GetString() : null,
                                    Line = constObj.TryGetProperty("line", out var line) ? line.GetInt32() : null,
                                    Purpose = constObj.TryGetProperty("purpose", out var purpose) ? purpose.GetString() : null
                                };
                            }
                        }

                        // Extract validation rules
                        if (flow.TryGetProperty("validationRules", out var rules))
                        {
                            results.ValidationRules.AddRange(
                                rules.EnumerateArray().Select(r => r.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s))
                            );
                        }

                        // Extract code snippets
                        if (flow.TryGetProperty("codeSnippets", out var snippets))
                        {
                            foreach (var snippet in snippets.EnumerateObject())
                            {
                                results.CodeSnippets.Add(new CodeSnippet
                                {
                                    Name = snippet.Name,
                                    Code = snippet.Value.GetString() ?? "",
                                    Language = "java", // Most snippets are Java
                                    Explanation = $"From {flowName}"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning("Failed to load flow {Flow}: {Error}", flowName, ex.Message);
                }
            }
        }

        // Determine if ambiguous
        results.IsAmbiguous = results.Operations.Count > 10 || results.Flows.Count > 3;
        results.TotalMatches = results.Operations.Count + results.Flows.Count;

        return results;
    }

    private static List<ResourceLinkBlock> BuildResourceLinks(SearchResults results)
    {
        var links = new List<ResourceLinkBlock>();

        // Link to flow details if flows found
        if (results.Flows.Any())
        {
            links.Add(new ResourceLinkBlock
            {
                Uri = $"mcp://stampli-acumatica/get_flow_details?flow={results.Flows.First().Name}",
                Name = $"Deep dive into {results.Flows.First().Name}"
            });
        }

        // Link to TDD workflow if operations found
        if (results.Operations.Any())
        {
            links.Add(new ResourceLinkBlock
            {
                Uri = $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={results.Operations.First().Method}",
                Name = $"Generate TDD workflow for {results.Operations.First().Method}"
            });
        }

        // Always link to flow recommender
        links.Add(new ResourceLinkBlock
        {
            Uri = "mcp://stampli-acumatica/recommend_flow?useCase=my integration need",
            Name = "Get AI-powered flow recommendation"
        });

        return links;
    }

    private class SearchResults
    {
        public List<OperationSummary> Operations { get; set; } = new();
        public List<FlowSummary> Flows { get; set; } = new();
        public Dictionary<string, ConstantInfo> Constants { get; set; } = new();
        public List<CodeSnippet> CodeSnippets { get; set; } = new();
        public List<string> ValidationRules { get; set; } = new();
        public bool IsAmbiguous { get; set; }
        public int TotalMatches { get; set; }
    }
}
