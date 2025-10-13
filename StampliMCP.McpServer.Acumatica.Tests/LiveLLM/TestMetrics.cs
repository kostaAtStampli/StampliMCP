using System;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Metrics collected from LLM test runs
/// </summary>
public record TestMetrics
{
    public string TestName { get; init; } = string.Empty;
    public string LLMType { get; init; } = string.Empty; // "claude", "gpt-4o", etc.
    public bool Success { get; init; }
    public string? FailureReason { get; init; }

    // Timing
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public TimeSpan Duration => EndTime - StartTime;

    // Conversation metrics
    public int TotalTurns { get; init; }
    public int ToolCalls { get; init; }
    public int FileWrites { get; init; }
    public int FileReads { get; init; }

    // Token usage (if available)
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens => InputTokens + OutputTokens;

    // Cost estimation (USD)
    public decimal EstimatedCost { get; init; }

    // Files created
    public List<string> FilesCreated { get; init; } = new();

    // Workflow phases reached
    public List<string> PhasesCompleted { get; init; } = new();
    public string FinalPhase { get; init; } = string.Empty;

    /// <summary>
    /// Calculate cost based on LLM type and token usage
    /// </summary>
    public static decimal CalculateCost(string llmType, long inputTokens, long outputTokens)
    {
        // Pricing as of 2025 (per 1M tokens)
        return llmType.ToLower() switch
        {
            "claude-3-5-sonnet" => (inputTokens * 0.003m + outputTokens * 0.015m) / 1_000,
            "claude-3-haiku" => (inputTokens * 0.00025m + outputTokens * 0.00125m) / 1_000,
            "gpt-4o" => (inputTokens * 0.005m + outputTokens * 0.015m) / 1_000,
            "gpt-4o-mini" => (inputTokens * 0.00015m + outputTokens * 0.0006m) / 1_000,
            _ => 0m
        };
    }

    /// <summary>
    /// Generate a summary report
    /// </summary>
    public string ToSummary()
    {
        return $@"
Test: {TestName}
LLM: {LLMType}
Status: {(Success ? "✓ SUCCESS" : "✗ FAILED" + (FailureReason != null ? $" - {FailureReason}" : ""))}
Duration: {Duration.TotalSeconds:F1}s
Turns: {TotalTurns}
Tool Calls: {ToolCalls}
Files Created: {FilesCreated.Count}
Phases: {string.Join(" → ", PhasesCompleted)} → {FinalPhase}
Tokens: {TotalTokens:N0} ({InputTokens:N0} in / {OutputTokens:N0} out)
Cost: ${EstimatedCost:F4}
";
    }
}

/// <summary>
/// Aggregates metrics from multiple test runs
/// </summary>
public class MetricsAggregator
{
    private readonly List<TestMetrics> _metrics = new();

    public void Add(TestMetrics metrics)
    {
        _metrics.Add(metrics);
    }

    public string GenerateReport()
    {
        if (_metrics.Count == 0)
            return "No metrics collected";

        var successful = _metrics.Count(m => m.Success);
        var failed = _metrics.Count - successful;
        var avgDuration = _metrics.Average(m => m.Duration.TotalSeconds);
        var avgTurns = _metrics.Average(m => m.TotalTurns);
        var totalCost = _metrics.Sum(m => m.EstimatedCost);

        var byLLM = _metrics.GroupBy(m => m.LLMType)
            .Select(g => new
            {
                LLM = g.Key,
                Success = g.Count(m => m.Success),
                Total = g.Count(),
                AvgDuration = g.Average(m => m.Duration.TotalSeconds),
                AvgCost = g.Average(m => m.EstimatedCost)
            });

        var report = $@"
=== LLM Test Metrics Report ===
Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}

Overall:
  Total Tests: {_metrics.Count}
  Successful: {successful} ({successful * 100.0 / _metrics.Count:F1}%)
  Failed: {failed}
  Avg Duration: {avgDuration:F1}s
  Avg Turns: {avgTurns:F1}
  Total Cost: ${totalCost:F2}

By LLM:
";

        foreach (var llm in byLLM)
        {
            report += $@"
  {llm.LLM}:
    Success Rate: {llm.Success}/{llm.Total} ({llm.Success * 100.0 / llm.Total:F1}%)
    Avg Duration: {llm.AvgDuration:F1}s
    Avg Cost: ${llm.AvgCost:F4}
";
        }

        if (failed > 0)
        {
            report += "\nFailures:\n";
            foreach (var failure in _metrics.Where(m => !m.Success))
            {
                report += $"  - {failure.TestName}: {failure.FailureReason}\n";
            }
        }

        return report;
    }

    public void SaveReport(string filePath)
    {
        File.WriteAllText(filePath, GenerateReport());
    }
}
