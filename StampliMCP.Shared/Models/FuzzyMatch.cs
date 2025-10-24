namespace StampliMCP.Shared.Models;

/// <summary>
/// Result of a fuzzy string match using Levenshtein distance
/// </summary>
/// <param name="Pattern">The matched pattern</param>
/// <param name="Distance">Levenshtein distance from query (lower = better)</param>
/// <param name="Confidence">Confidence score 0.0-1.0 (higher = better). Normalized: 1.0 - (distance / maxLength)</param>
public sealed record FuzzyMatch(string Pattern, int Distance, double Confidence);
