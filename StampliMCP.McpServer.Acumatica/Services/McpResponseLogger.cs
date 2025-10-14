using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Logs MCP tool executions in format expected by test ground truth validation.
/// Separate from JsonFileLogger (OpenTelemetry) - this is for test verification.
/// </summary>
public class McpResponseLogger
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Log a tool execution for ground truth validation
    /// </summary>
    public async Task LogToolExecutionAsync(
        string tool,
        string command,
        string context,
        string? flowName,
        int responseSize,
        int operationCount)
    {
        await _lock.WaitAsync();
        try
        {
            // Support per-test isolation via environment variable (same as JsonFileLogger)
            var logDir = Environment.GetEnvironmentVariable("MCP_LOG_DIR")
                         ?? Path.Combine(Path.GetTempPath(), "mcp_logs");
            Directory.CreateDirectory(logDir);

            // Daily log file for test validation
            var logFile = Path.Combine(logDir, $"mcp_responses_{DateTime.Now:yyyyMMdd}.jsonl");

            var entry = new
            {
                timestamp = DateTimeOffset.UtcNow.ToString("O"),
                tool,
                command,
                context,
                flowName = flowName ?? "",
                responseSize,
                operationCount
            };

            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            await File.AppendAllTextAsync(logFile, json + "\n");
        }
        finally
        {
            _lock.Release();
        }
    }
}
