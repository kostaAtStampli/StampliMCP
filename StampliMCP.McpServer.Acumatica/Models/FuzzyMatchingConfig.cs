namespace StampliMCP.McpServer.Acumatica.Models;

/// <summary>
/// Configuration for fuzzy string matching thresholds
/// GENEROUS thresholds (0.60-0.70) - catch more, trust less
/// </summary>
public sealed class FuzzyMatchingConfig
{
    /// <summary>
    /// Default fuzzy match threshold - 60% similarity
    /// </summary>
    public double DefaultThreshold { get; init; } = 0.60;

    /// <summary>
    /// Typo tolerance threshold - 70% similarity (1-3 char typos)
    /// </summary>
    public double TypoToleranceThreshold { get; init; } = 0.70;

    /// <summary>
    /// Operation name matching threshold - 60% similarity
    /// </summary>
    public double OperationMatchThreshold { get; init; } = 0.60;

    /// <summary>
    /// Error message matching threshold - 65% similarity
    /// </summary>
    public double ErrorMatchThreshold { get; init; } = 0.65;

    /// <summary>
    /// Flow detection threshold - 60% similarity
    /// </summary>
    public double FlowMatchThreshold { get; init; } = 0.60;

    /// <summary>
    /// Keyword matching threshold - 60% similarity
    /// </summary>
    public double KeywordMatchThreshold { get; init; } = 0.60;
}
