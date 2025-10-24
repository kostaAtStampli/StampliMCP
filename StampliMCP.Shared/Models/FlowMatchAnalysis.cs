using System.Collections.Generic;

namespace StampliMCP.Shared.Models;

public sealed class FlowMatchCandidate
{
    public string FlowName { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public double ActionScore { get; set; }
    public double EntityScore { get; set; }
    public double KeywordScore { get; set; }
    public string ConfidenceLabel { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
}

public sealed class FlowMatchAnalysis
{
    public FlowMatchCandidate Primary { get; set; } = new();
    public List<FlowMatchCandidate> Alternatives { get; set; } = new();
}
