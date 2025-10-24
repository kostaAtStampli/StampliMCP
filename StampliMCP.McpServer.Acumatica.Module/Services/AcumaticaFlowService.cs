using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using StampliMCP.Shared.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Acumatica-specific flow service
/// Inherits generic flow loading from base class
/// Adds Acumatica-specific flow matching logic
/// </summary>
public sealed class AcumaticaFlowService : FlowServiceBase
{
    protected override string FlowResourcePrefix => "StampliMCP.McpServer.Acumatica.Module.Knowledge.flows";

    private readonly SmartFlowMatcher _matcher;
    private readonly IReadOnlyList<FlowSignature> _signatures;

    public AcumaticaFlowService(
        ILogger<AcumaticaFlowService> logger,
        IMemoryCache cache,
        FuzzyMatchingService fuzzyMatcher,
        SmartFlowMatcher matcher,
        IReadOnlyList<FlowSignature> signatures)
        : base(logger, cache, fuzzyMatcher, typeof(AcumaticaFlowService).Assembly)
    {
        _matcher = matcher;
        _signatures = signatures;
    }

    /// <summary>
    /// Analyze a natural language description and rank flows using action/entity/keyword scoring.
    /// </summary>
    public FlowMatchAnalysis MatchFeatureToFlow(string description, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _matcher.AnalyzeQuery(description);
        var lower = description.ToLowerInvariant();

        var candidates = new List<FlowMatchCandidate>();
        foreach (var signature in _signatures)
        {
            var actionScore = ScoreList(tokens.Actions, signature.Actions);
            var entityScore = ScoreList(tokens.Entities, signature.Entities);
            var (keywordScore, keywordHits) = ScoreKeywords(lower, signature.Keywords);

            var overall = (entityScore * 0.6) + (actionScore * 0.3) + (keywordScore * 0.1);
            var confidenceLabel = MapConfidence(overall);
            var reasoning = BuildReasoning(signature, tokens, keywordHits, actionScore, entityScore, keywordScore);

            candidates.Add(new FlowMatchCandidate
            {
                FlowName = signature.Name,
                OverallScore = overall,
                ActionScore = actionScore,
                EntityScore = entityScore,
                KeywordScore = keywordScore,
                ConfidenceLabel = confidenceLabel,
                Reasoning = reasoning
            });
        }

        if (candidates.Count == 0)
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: no signatures configured, defaulting to standard_import_flow");
            return new FlowMatchAnalysis
            {
                Primary = new FlowMatchCandidate
                {
                    FlowName = "standard_import_flow",
                    OverallScore = 0.4,
                    ActionScore = 0.4,
                    EntityScore = 0.4,
                    KeywordScore = 0.4,
                    ConfidenceLabel = MapConfidence(0.4),
                    Reasoning = "No flow signatures configured; defaulting to standard import"
                }
            };
        }

        var ordered = candidates
            .OrderByDescending(c => c.OverallScore)
            .ThenByDescending(c => c.EntityScore)
            .ThenByDescending(c => c.ActionScore)
            .ToList();

        var primary = ordered.First();
        var alternatives = ordered
            .Skip(1)
            .Where(c => c.OverallScore >= 0.25)
            .Take(4)
            .ToList();

        sw.Stop();
        Logger.LogInformation("FlowMatch: {Flow} overall={Score:P0} action={Action:P0} entity={Entity:P0} keywords={Keywords:P0} time={Ms}ms",
            primary.FlowName, primary.OverallScore, primary.ActionScore, primary.EntityScore, primary.KeywordScore, sw.ElapsedMilliseconds);

        return new FlowMatchAnalysis
        {
            Primary = primary,
            Alternatives = alternatives
        };
    }

    private static double ScoreList(IEnumerable<string> tokens, IEnumerable<string> expected)
    {
        var expectedList = expected?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        if (expectedList.Count == 0)
        {
            return 0.4; // neutral baseline when no expectation defined
        }

        var tokenSet = new HashSet<string>(tokens ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var hits = expectedList.Count(tokenSet.Contains);
        return expectedList.Count == 0 ? 0.0 : (double)hits / expectedList.Count;
    }

    private (double Score, List<string> Hits) ScoreKeywords(string lowerQuery, IEnumerable<string> keywords)
    {
        var keywordList = keywords?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        if (keywordList.Count == 0)
        {
            return (0.3, new List<string>()); // slight baseline if no keywords configured
        }

        var hits = keywordList
            .Where(k => ContainsOrFuzzy(lowerQuery, k, 0.60))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var score = (double)hits.Count / keywordList.Count;
        return (score, hits);
    }

    private static string MapConfidence(double score)
    {
        if (score >= 0.75)
        {
            return "HIGH";
        }

        if (score >= 0.5)
        {
            return "MEDIUM";
        }

        return "LOW";
    }

    private static string BuildReasoning(FlowSignature signature, FlowMatch tokens, List<string> keywordHits, double actionScore, double entityScore, double keywordScore)
    {
        var reasons = new List<string>();

        if (signature.Entities.Count > 0 && entityScore > 0)
        {
            var matchedEntities = tokens.Entities
                .Where(e => signature.Entities.Contains(e, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            reasons.Add($"entities {string.Join('/', matchedEntities)}");
        }

        if (signature.Actions.Count > 0 && actionScore > 0)
        {
            var matchedActions = tokens.Actions
                .Where(a => signature.Actions.Contains(a, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            reasons.Add($"actions {string.Join('/', matchedActions)}");
        }

        if (keywordScore > 0 && keywordHits.Count > 0)
        {
            reasons.Add($"keywords {string.Join('/', keywordHits)}");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("matched generic import/export language");
        }

        return string.Join(", ", reasons);
    }
}
