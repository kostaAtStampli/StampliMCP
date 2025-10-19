using System.Diagnostics;
using Fastenshtein;
using Microsoft.Extensions.Logging;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Fuzzy string matching service using Fastenshtein (optimal Levenshtein)
/// NO CACHING - Simple, direct usage pattern
/// OPTIMAL PATTERN: Create Levenshtein(query), compare against many patterns
/// </summary>
public sealed class FuzzyMatchingService
{
    private readonly FuzzyMatchingConfig _config;
    private readonly ILogger<FuzzyMatchingService> _logger;

    public FuzzyMatchingService(FuzzyMatchingConfig config, ILogger<FuzzyMatchingService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Find all patterns matching query above threshold (OPTIMAL: one query, many patterns)
    /// </summary>
    public List<FuzzyMatch> FindAllMatches(string query, IEnumerable<string> patterns, double threshold)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<FuzzyMatch>();

        var sw = Stopwatch.StartNew();
        var patternList = patterns.ToList();

        // OPTIMAL FASTENSHTEIN USAGE:
        // Create ONE Levenshtein instance with query, compare against MANY patterns
        // This is 7x faster than creating instance per pattern
        var queryMatcher = new Levenshtein(query.ToLower());

        var results = patternList
            .Select(p =>
            {
                var distance = queryMatcher.DistanceFrom(p.ToLower());
                var confidence = DistanceToConfidence(distance, Math.Max(p.Length, query.Length));
                return new FuzzyMatch(p, distance, confidence);
            })
            .Where(r => r.Confidence >= threshold)
            .OrderByDescending(r => r.Confidence)
            .ToList();

        sw.Stop();
        _logger.LogInformation(
            "FuzzyMatch: query=\"{Query}\", patterns={PatternCount}, matches={MatchCount}, threshold={Threshold:P0}, time={ElapsedMs}ms",
            query, patternList.Count, results.Count, threshold, sw.ElapsedMilliseconds);

        return results;
    }

    /// <summary>
    /// Find best matching pattern above threshold
    /// </summary>
    public FuzzyMatch? FindBestMatch(string query, IEnumerable<string> patterns, double threshold)
    {
        var matches = FindAllMatches(query, patterns, threshold);
        return matches.FirstOrDefault();
    }

    /// <summary>
    /// Find best match with custom threshold
    /// </summary>
    public FuzzyMatch? FindBestMatch(string query, IEnumerable<string> patterns, double threshold, out List<FuzzyMatch> allMatches)
    {
        allMatches = FindAllMatches(query, patterns, threshold);
        return allMatches.FirstOrDefault();
    }

    /// <summary>
    /// Check if query matches pattern with default threshold
    /// </summary>
    public bool IsMatch(string query, string pattern)
    {
        return IsMatch(query, pattern, _config.DefaultThreshold);
    }

    /// <summary>
    /// Check if query matches pattern with custom threshold
    /// </summary>
    public bool IsMatch(string query, string pattern, double threshold)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var queryLower = query.ToLower();
        var patternLower = pattern.ToLower();

        // Fast path: exact match
        if (queryLower == patternLower)
            return true;

        // Fuzzy match
        var matcher = new Levenshtein(queryLower);
        var distance = matcher.DistanceFrom(patternLower);
        var confidence = DistanceToConfidence(distance, Math.Max(pattern.Length, query.Length));

        return confidence >= threshold;
    }

    /// <summary>
    /// Calculate confidence score from Levenshtein distance
    /// </summary>
    private static double DistanceToConfidence(int distance, int maxLength)
    {
        if (maxLength == 0)
            return 0.0;

        // Normalize: distance 0 = 1.0 (perfect), distance = maxLength = 0.0 (no match)
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Get configured threshold for specific use case
    /// </summary>
    public double GetThreshold(string useCase)
    {
        return useCase.ToLower() switch
        {
            "typo" or "typos" => _config.TypoToleranceThreshold,
            "operation" or "operations" => _config.OperationMatchThreshold,
            "error" or "errors" => _config.ErrorMatchThreshold,
            "flow" or "flows" => _config.FlowMatchThreshold,
            "keyword" or "keywords" => _config.KeywordMatchThreshold,
            _ => _config.DefaultThreshold
        };
    }
}
