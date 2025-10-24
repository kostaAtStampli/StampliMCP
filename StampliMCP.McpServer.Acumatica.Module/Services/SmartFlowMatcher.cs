using System.Buffers;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Smart flow matcher using .NET 10 SearchValues for ultra-fast string matching + Fastenshtein for typo tolerance
/// </summary>
public sealed class SmartFlowMatcher
{
    private readonly FuzzyMatchingService _fuzzyMatcher;
    private readonly ILogger<SmartFlowMatcher> _logger;

    public SmartFlowMatcher(FuzzyMatchingService fuzzyMatcher, ILogger<SmartFlowMatcher> logger)
    {
        _fuzzyMatcher = fuzzyMatcher;
        _logger = logger;
    }
    // Pre-compiled SearchValues for ultra-fast matching (7x faster than Contains)
    private static readonly SearchValues<string> ActionWords = SearchValues.Create(
        ["import", "export", "sync", "send", "get", "fetch", "create", "retrieve", "pull", "push", "submit", "release"],
        StringComparison.OrdinalIgnoreCase);

    private static readonly SearchValues<string> EntityWords = SearchValues.Create(
        ["vendor", "vendors", "invoice", "invoices", "bill", "bills", "payment", "payments",
         "item", "items", "product", "products", "po", "purchase", "order", "orders", "transaction", "transactions"],
        StringComparison.OrdinalIgnoreCase);

    // Common aliases - map variations to canonical terms
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vendor"] = "vendor",
        ["vendors"] = "vendor",
        ["supplier"] = "vendor",
        ["suppliers"] = "vendor",
        ["payee"] = "vendor",
        ["payees"] = "vendor",
        ["seller"] = "vendor",
        ["sellers"] = "vendor",
        ["bill"] = "bill",
        ["bills"] = "bill",
        ["invoice"] = "bill",  // Acumatica uses "bill" terminology, not "invoice"
        ["invoices"] = "bill",
        ["ap"] = "bill",
        ["aptransaction"] = "bill",
        ["product"] = "item",
        ["products"] = "item",
        ["item"] = "item",
        ["items"] = "item",
        ["sku"] = "item",
        ["skus"] = "item",
        ["inventory"] = "item",
        ["inventories"] = "item",
        ["pay"] = "payment",
        ["pays"] = "payment",
        ["payment"] = "payment",
        ["payments"] = "payment",
        ["paying"] = "payment",
        ["purchaseorder"] = "po",
        ["purchaseorders"] = "po",
        ["order"] = "order",
        ["orders"] = "order",
        ["transaction"] = "transaction",
        ["transactions"] = "transaction"
    };

    public FlowMatch AnalyzeQuery(string query)
    {
        // 1. Normalize and tokenize
        var normalized = query.ToLowerInvariant();
        var words = normalized.Split([' ', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        // 2. Apply aliases - replace synonyms with canonical terms
        words = words.Select(w => Aliases.GetValueOrDefault(w, w)).ToArray();

        // 3. Extract actions and entities using SearchValues (7x faster!)
        var actions = words.Where(w => ActionWords.Contains(w)).ToList();
        var entitiesRaw = words.Where(w => EntityWords.Contains(w)).ToList();

        // 4. Normalize entities to singular canonical forms
        var entities = entitiesRaw.Select(e => Aliases.GetValueOrDefault(e, e)).Distinct().ToList();

        return new FlowMatch
        {
            Actions = actions,
            Entities = entities,
            Words = words,
            OriginalQuery = query
        };
    }

    /// <summary>
    /// Calculate typo distance using optimal Fastenshtein pattern
    /// FIXED: Now uses FuzzyMatchingService with correct pattern (query instance, compare against patterns)
    /// </summary>
    public double CalculateTypoDistance(string query, IEnumerable<string> commonPatterns)
    {
        var sw = Stopwatch.StartNew();

        // OPTIMAL: Use FuzzyMatchingService (creates ONE instance with query, compares against many patterns)
        var bestMatch = _fuzzyMatcher.FindBestMatch(query, commonPatterns, _fuzzyMatcher.GetThreshold("typo"));

        sw.Stop();
        _logger.LogInformation(
            "TypoDistance: query=\"{Query}\", patterns={PatternCount}, bestMatch={BestMatch}, confidence={Confidence:P0}, time={ElapsedMs}ms",
            query, commonPatterns.Count(), bestMatch?.Pattern ?? "NONE", bestMatch?.Confidence ?? 0.0, sw.ElapsedMilliseconds);

        return bestMatch?.Confidence ?? 0.0;
    }

    /// <summary>
    /// Get all fuzzy matches for query against patterns with per-pattern confidence
    /// </summary>
    public List<FuzzyMatch> GetAllFuzzyMatches(string query, IEnumerable<string> patterns, double threshold)
    {
        return _fuzzyMatcher.FindAllMatches(query, patterns, threshold);
    }
}

public class FlowMatch
{
    public List<string> Actions { get; set; } = new();
    public List<string> Entities { get; set; } = new();
    public string[] Words { get; set; } = Array.Empty<string>();
    public string OriginalQuery { get; set; } = string.Empty;
}
