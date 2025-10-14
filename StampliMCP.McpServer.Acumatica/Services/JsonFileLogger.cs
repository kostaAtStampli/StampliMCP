using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Simple thread-safe JSON file logger. No dependencies, works with PublishSingleFile.
/// </summary>
public class JsonFileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileLogger()
    {
        var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
        Directory.CreateDirectory(logDir);

        var logFile = Path.Combine(logDir, "structured.jsonl");
        _writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
    }

    public async Task WriteAsync(object logEntry)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            await _writer.WriteLineAsync(json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _lock?.Dispose();
    }
}
