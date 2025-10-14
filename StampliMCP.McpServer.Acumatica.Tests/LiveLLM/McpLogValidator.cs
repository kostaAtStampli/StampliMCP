using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Parses MCP logs to extract ground truth for semantic verification
/// 2025 AI-first approach: Verify LLM claims against provable facts
/// </summary>
public static class McpLogValidator
{
    /// <summary>
    /// Parse the latest MCP log entry to get ground truth
    /// </summary>
    public static McpGroundTruth ParseLatestLog(string? logDir = null)
    {
        // PRIMARY: Try test-isolated directory first (if provided)
        var tempPath = logDir ?? Path.Combine(Path.GetTempPath(), "mcp_logs");
        if (!Directory.Exists(tempPath))
        {
            tempPath = Path.GetTempPath();
        }

        var logFiles = Directory.GetFiles(tempPath, "mcp_*.jsonl");

        // FALLBACK: If no logs in test directory, try FIXED location
        if (logFiles.Length == 0 && logDir != null)
        {
            Console.WriteLine($"[McpLogValidator] No logs in test directory: {logDir}");
            var fixedLogDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
            Console.WriteLine($"[McpLogValidator] Falling back to FIXED location: {fixedLogDir}");

            if (Directory.Exists(fixedLogDir))
            {
                tempPath = fixedLogDir;
                logFiles = Directory.GetFiles(tempPath, "mcp_*.jsonl");
            }
        }

        if (logFiles.Length == 0)
        {
            throw new FileNotFoundException($"No MCP logs found in {tempPath}. MCP tool may not have been called.");
        }

        var latestLog = logFiles.OrderByDescending(File.GetLastWriteTime).First();
        Console.WriteLine($"[McpLogValidator] Reading ground truth from: {latestLog}");

        var lastEntry = File.ReadLines(latestLog).Last();

        var logEntry = JsonSerializer.Deserialize<McpLogEntry>(lastEntry);
        if (logEntry == null)
        {
            throw new InvalidOperationException("Failed to parse MCP log entry");
        }

        return new McpGroundTruth
        {
            Tool = logEntry.Tool,
            Command = logEntry.Command,
            Context = logEntry.Context,
            FlowName = logEntry.FlowName,
            OperationCount = logEntry.OperationCount,
            ResponseSize = logEntry.ResponseSize,
            Timestamp = DateTime.Parse(logEntry.Timestamp)
        };
    }

}

/// <summary>
/// Ground truth data extracted from MCP logs
/// </summary>
public record McpGroundTruth
{
    public required string Tool { get; init; }
    public required string Command { get; init; }
    public required string Context { get; init; }
    public required string FlowName { get; init; }
    public required int OperationCount { get; init; }
    public required int ResponseSize { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// MCP log entry structure (matches KotlinTddWorkflowTool.cs logging format)
/// Simplified to match actual flat log format
/// </summary>
public record McpLogEntry
{
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("context")]
    public required string Context { get; init; }

    [JsonPropertyName("flowName")]
    public required string FlowName { get; init; }

    [JsonPropertyName("responseSize")]
    public required int ResponseSize { get; init; }

    [JsonPropertyName("operationCount")]
    public required int OperationCount { get; init; }
}
