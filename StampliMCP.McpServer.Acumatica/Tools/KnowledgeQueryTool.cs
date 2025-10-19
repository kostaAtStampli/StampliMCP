using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica;

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
    public static async Task<CallToolResult> Execute(
        [Description("Query: operation name, entity type, pattern, or natural language question")]
        string query,

        [Description("Optional scope filter: operations, flows, constants, or all")]
        string? scope,

        ModelContextProtocol.Server.McpServer server, // Schema-based elicitation via McpServer (current SDK)
        KnowledgeService knowledge,
        FlowService flowService,
        FuzzyMatchingService fuzzyMatcher,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: query={Query}, scope={Scope}",
            "query_acumatica_knowledge", query, scope ?? "all");

        try
        {
            // Step 1: Search knowledge base (with fuzzy token matching)
            var searchResults = await SearchKnowledge(query, scope, knowledge, flowService, fuzzyMatcher, ct);

            // Step 2: Try elicitation if ambiguous (Protocol 2025-06-18 - schema-based API)
            // If elicitation not supported, continue with results
            if (searchResults.IsAmbiguous)
            {
                try
                {
                    var message = searchResults.Operations.Any() && searchResults.Flows.Any()
                        ? $"Found {searchResults.TotalMatches} matches ({searchResults.Operations.Count} operations, {searchResults.Flows.Count} flows). Refine search:"
                        : searchResults.Operations.Any()
                        ? $"Found {searchResults.Operations.Count} operations. Provide more specific terms:"
                        : $"Found {searchResults.Flows.Count} flows. Provide more specific terms:";

                    var schema = new ElicitRequestParams.RequestSchema
                    {
                        Properties =
                        {
                            ["type"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Scope to filter (operations, flows, constants, or all)"
                            },
                            ["refinement"] = new ElicitRequestParams.StringSchema
                            {
                                Description = "Additional keywords to narrow results"
                            }
                        }
                    };

                    var elicitResult = await server.ElicitAsync(new ElicitRequestParams
                    {
                        Message = message,
                        RequestedSchema = schema
                    }, ct);

                    if (elicitResult.Action == "accept" && elicitResult.Content is { } content)
                    {
                        string? type = null;
                        string? refinement = null;

                        if (content.TryGetValue("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                            type = typeEl.GetString();
                        if (content.TryGetValue("refinement", out var refEl) && refEl.ValueKind == JsonValueKind.String)
                            refinement = refEl.GetString();

                        var refinedScope = !string.IsNullOrEmpty(type) && type != "all" ? type : scope;
                        var refinedQuery = !string.IsNullOrWhiteSpace(refinement) ? $"{query} {refinement}" : query;

                        searchResults = await SearchKnowledge(refinedQuery, refinedScope, knowledge, flowService, fuzzyMatcher, ct);
                    }
                }
                catch (Exception ex)
                {
                    // Elicitation not supported - continue with ambiguous results
                    Serilog.Log.Warning("Elicitation not supported in query_acumatica_knowledge: {Message}", ex.Message);
                }
            }

            // Step 3: Build structured result
            var structured = new KnowledgeQueryResult
            {
                MatchedOperations = searchResults.Operations,
                RelevantFlows = searchResults.Flows,
                Constants = searchResults.Constants,
                CodeExamples = searchResults.CodeSnippets,
                ValidationRules = searchResults.ValidationRules,
                NextActions = BuildResourceLinks(searchResults),
                Summary = $"Found {searchResults.TotalMatches} matches (ops={searchResults.Operations.Count}, flows={searchResults.Flows.Count}) {BuildInfo.Marker}"
            };

            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = structured });

            // Serialize full structured content as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(structured, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });

            // Convert resource links into content
            foreach (var link in structured.NextActions)
            {
                ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
            }

            Serilog.Log.Information("Tool {Tool} completed: ops={OpCount}, flows={FlowCount}",
                "query_acumatica_knowledge", structured.MatchedOperations.Count, structured.RelevantFlows.Count);

            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "query_acumatica_knowledge", ex.Message);

            var errorResult = new
            {
                error = $"Query failed: {ex.Message}",
                marker = BuildInfo.Marker,
                suggestion = "Try calling health_check to verify server status"
            };

            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = errorResult });

            // Serialize error as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
            ret.Content.Add(new ResourceLinkBlock { Uri = "mcp://stampli-acumatica/health_check", Name = "Check server health" });
            return ret;
        }
    }

    private static async Task<SearchResults> SearchKnowledge(
        string query,
        string? scope,
        KnowledgeService knowledge,
        FlowService flowService,
        FuzzyMatchingService fuzzyMatcher,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var results = new SearchResults();
        var lowerQuery = (query ?? string.Empty).ToLower();
        var isWildcard = string.IsNullOrWhiteSpace(lowerQuery) || lowerQuery == "*";

        // Tokenize query for better matching (e.g., "vendor export" → ["vendor", "export"]). Treat empty or "*" as wildcard.
        var queryTokens = isWildcard
            ? Array.Empty<string>()
            : lowerQuery.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

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

                    // Fast path: Match if ANY token appears in method, summary, or category
                    bool exactMatch = queryTokens.Any(token =>
                        opMethodLower.Contains(token) ||
                        opSummaryLower.Contains(token) ||
                        categoryLower.Contains(token)
                    );

                    if (exactMatch) return true;

                    // Fuzzy path: Check if any token fuzzy-matches
                    foreach (var token in queryTokens)
                    {
                        var searchWords = $"{opMethodLower} {opSummaryLower} {categoryLower}".Split([' ', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
                        var fuzzyMatches = fuzzyMatcher.FindAllMatches(token, searchWords, fuzzyMatcher.GetThreshold("keyword"));
                        if (fuzzyMatches.Any())
                            return true;
                    }

                    return false;
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

        // Search constants specifically if requested
        if (scope == "constants")
        {
            // Load constants from all known flows dynamically
            var flowNames = await flowService.GetAllFlowNamesAsync(ct);
            foreach (var flowName in flowNames)
            {
                try
                {
                    var flowDoc = await flowService.GetFlowAsync(flowName, ct);
                    if (flowDoc == null) continue;

                    var flow = flowDoc.RootElement;

                    // Extract constants matching query
                    if (flow.TryGetProperty("constants", out var constants))
                    {
                        foreach (var constant in constants.EnumerateObject())
                        {
                            var constNameLower = constant.Name.ToLower();
                            var constObj = constant.Value;
                            var valueLower = constObj.TryGetProperty("value", out var val) ? val.ToString().ToLower() : "";
                            var purposeLower = constObj.TryGetProperty("purpose", out var purpose) ? (purpose.GetString() ?? "").ToLower() : "";

                            // Match if wildcard OR any token appears in name, value, or purpose
                            bool matches = queryTokens.Length == 0 || queryTokens.Any(token =>
                                constNameLower.Contains(token) ||
                                valueLower.Contains(token) ||
                                purposeLower.Contains(token)
                            );

                            if (matches)
                            {
                                results.Constants[constant.Name] = new ConstantInfo
                                {
                                    Name = constant.Name,
                                    Value = constObj.TryGetProperty("value", out var v) ? v.ToString() : "",
                                    File = constObj.TryGetProperty("file", out var file) ? file.GetString() : null,
                                    Line = constObj.TryGetProperty("line", out var line) ? line.GetInt32() : null,
                                    Purpose = constObj.TryGetProperty("purpose", out var p) ? p.GetString() : null
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning("Failed to load flow {Flow} for constants: {Error}", flowName, ex.Message);
                }
            }
        }

        // Search flows
        if (scope == null || scope == "all" || scope == "flows")
        {
            var flowNames = await flowService.GetAllFlowNamesAsync(ct);
            foreach (var flowName in flowNames)
            {
                try
                {
                    var flowDoc = await flowService.GetFlowAsync(flowName, ct);
                    if (flowDoc == null) continue;

                    var flow = flowDoc.RootElement;
                    var description = flow.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
                    var flowNameLower = flowName.ToLower();
                    var descriptionLower = description.ToLower();

                    // Match flow by name or description (using tokens); if no tokens, list all
                    bool matches = queryTokens.Length == 0 || queryTokens.Any(token =>
                        flowNameLower.Contains(token) ||
                        descriptionLower.Contains(token)
                    );

                    if (matches)
                    {
                        results.Flows.Add(new FlowSummary
                        {
                            Name = flowName,
                            Description = description,
                            UsedByOperations = flow.TryGetProperty("usedByOperations", out var ubo)
                                ? ubo.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                                : new List<string>()
                        });

                        // Also hydrate matched operations from this flow
                        if (flow.TryGetProperty("usedByOperations", out var usedBy))
                        {
                            foreach (var opNameEl in usedBy.EnumerateArray())
                            {
                                var opName = opNameEl.GetString();
                                if (string.IsNullOrWhiteSpace(opName)) continue;
                                var op = await knowledge.FindOperationAsync(opName!, ct);
                                if (op != null && !results.Operations.Any(o => o.Method.Equals(op.Method, StringComparison.OrdinalIgnoreCase)))
                                {
                                    results.Operations.Add(new OperationSummary
                                    {
                                        Method = op.Method,
                                        Summary = op.Summary ?? string.Empty,
                                        Category = op.Category,
                                        Flow = flowName
                                    });
                                }
                            }
                        }

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

        sw.Stop();
        Serilog.Log.Information("SearchKnowledge: query=\"{Query}\", scope={Scope}, matches={Total}, time={Ms}ms",
            query, scope ?? "all", results.TotalMatches, sw.ElapsedMilliseconds);

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

        // Temporary verification marker
        links.Add(new ResourceLinkBlock
        {
            Uri = "mcp://stampli-acumatica/marker",
            Name = BuildInfo.Marker,
            Description = $"build={BuildInfo.VersionTag}"
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
