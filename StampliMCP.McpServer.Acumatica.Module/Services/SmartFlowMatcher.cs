using System.Buffers;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Smart flow matcher using .NET 10 SearchValues for ultra-fast string matching + Fastenshtein for typo tolerance
/// </summary>
public sealed class SmartFlowMatcher
{
    private readonly FuzzyMatchingService _fuzzyMatcher;
    private readonly ILogger<SmartFlowMatcher> _logger;

    private readonly SearchValues<string> _actionWords;
    private readonly SearchValues<string> _entityWords;
    private readonly Dictionary<string, string> _aliases;

    public SmartFlowMatcher(FuzzyMatchingService fuzzyMatcher, ILogger<SmartFlowMatcher> logger, FlowMatchingConfiguration configuration)
    {
        _fuzzyMatcher = fuzzyMatcher;
        _logger = logger;

        configuration ??= FlowMatchingConfiguration.CreateDefault();
        var defaults = FlowMatchingConfiguration.CreateDefault();

        var actionWords = (configuration.ActionWords?.Count > 0 ? configuration.ActionWords : defaults.ActionWords)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entityWords = (configuration.EntityWords?.Count > 0 ? configuration.EntityWords : defaults.EntityWords)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _actionWords = SearchValues.Create(actionWords, StringComparison.OrdinalIgnoreCase);
        _entityWords = SearchValues.Create(entityWords, StringComparison.OrdinalIgnoreCase);

        var aliasSource = configuration.Aliases is { Count: > 0 } ? configuration.Aliases : defaults.Aliases;
        _aliases = new Dictionary<string, string>(aliasSource, StringComparer.OrdinalIgnoreCase);
    }

    public FlowMatch AnalyzeQuery(string query)
    {
        // 1. Normalize and tokenize
        var normalized = query.ToLowerInvariant();
        var originalWords = normalized.Split([' ', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        // 2. Apply aliases - replace synonyms with canonical terms
        var words = originalWords
            .Select(w => _aliases.TryGetValue(w, out var mapped) ? mapped : w)
            .ToArray();

        // 3. Extract actions and entities using SearchValues (ultra-fast lookup)
        var actions = words
            .Where(w => _actionWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entities = words
            .Where(w => _entityWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
