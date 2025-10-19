using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class SearchService
{
    private readonly KnowledgeService _knowledge;
    private readonly FuzzyMatchingService _fuzzyMatcher;
    private readonly ILogger<SearchService> _logger;

    public SearchService(KnowledgeService knowledge, FuzzyMatchingService fuzzyMatcher, ILogger<SearchService> logger)
    {
        _knowledge = knowledge;
        _fuzzyMatcher = fuzzyMatcher;
        _logger = logger;
    }

    /// <summary>
    /// Search operations with exact matching
    /// </summary>
    public async Task<List<(string Operation, string Match)>> SearchAsync(string query, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<(string, string)>();
        var categories = await _knowledge.GetCategoriesAsync(ct);

        foreach (var category in categories)
        {
            var ops = await _knowledge.GetOperationsByCategoryAsync(category.Name, ct);

            foreach (var op in ops.Where(o =>
                o.Method.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                o.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add((op.Method, $"{op.Summary[..Math.Min(80, op.Summary.Length)]}..."));
            }
        }

        sw.Stop();
        _logger.LogInformation("SearchAsync(exact): query=\"{Query}\", results={Count}, time={Ms}ms",
            query, results.Count, sw.ElapsedMilliseconds);

        return results;
    }

    /// <summary>
    /// Search operations with fuzzy matching (handles typos)
    /// Threshold: 0.60 (generous - catches more typos)
    /// </summary>
    public async Task<List<(string Operation, string Match, double Confidence)>> SearchFuzzyAsync(string query, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<(string, string, double)>();
        var categories = await _knowledge.GetCategoriesAsync(ct);
        var allOperations = new List<Models.Operation>();

        // Collect all operations
        foreach (var category in categories)
        {
            var ops = await _knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            allOperations.AddRange(ops);
        }

        // Fast path: Try exact match first
        var exactMatches = allOperations.Where(o =>
            o.Method.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            o.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (exactMatches.Any())
        {
            results.AddRange(exactMatches.Select(op =>
                (op.Method, $"{op.Summary[..Math.Min(80, op.Summary.Length)]}...", 1.0)));
        }
        else
        {
            // Fuzzy path: Use Fastenshtein for typo tolerance
            var operationNames = allOperations.Select(o => o.Method).ToList();
            var fuzzyMatches = _fuzzyMatcher.FindAllMatches(query, operationNames, _fuzzyMatcher.GetThreshold("operation"));

            foreach (var match in fuzzyMatches.Take(10)) // Limit to top 10 fuzzy matches
            {
                var op = allOperations.FirstOrDefault(o => o.Method == match.Pattern);
                if (op != null)
                {
                    results.Add((op.Method, $"{op.Summary[..Math.Min(80, op.Summary.Length)]}...", match.Confidence));
                }
            }
        }

        sw.Stop();
        _logger.LogInformation("SearchFuzzyAsync: query=\"{Query}\", results={Count}, fuzzy={IsFuzzy}, time={Ms}ms",
            query, results.Count, !exactMatches.Any(), sw.ElapsedMilliseconds);

        return results;
    }
}

