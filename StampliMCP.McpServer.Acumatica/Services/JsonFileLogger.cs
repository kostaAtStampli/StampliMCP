using System.Diagnostics;
using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Simple thread-safe JSON file logger with OpenTelemetry format support.
/// No dependencies, works with PublishSingleFile.
/// </summary>
public class JsonFileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileLogger()
    {
        // Support per-test isolation via environment variable (set by tests)
        // Falls back to shared location for production use
        var logDir = Environment.GetEnvironmentVariable("MCP_LOG_DIR")
                     ?? Path.Combine(Path.GetTempPath(), "mcp_logs");
        Directory.CreateDirectory(logDir);

        var logFile = Path.Combine(logDir, "structured.jsonl");
        _writer = new StreamWriter(logFile, append: true) { AutoFlush = true };
    }

    public async Task WriteAsync(object logEntry)
    {
        await _lock.WaitAsync();
        try
        {
            // Convert to OpenTelemetry Log Record format
            var otlpLog = ConvertToOTelFormat(logEntry);

            var json = JsonSerializer.Serialize(otlpLog, new JsonSerializerOptions
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

    private static object ConvertToOTelFormat(object logEntry)
    {
        // Extract level if present (for severity mapping)
        string? level = null;
        Dictionary<string, object?> attributes = new();

        if (logEntry is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            if (jsonElement.TryGetProperty("Level", out var levelProp))
            {
                level = levelProp.GetString();
            }

            // Extract semantic attributes from log entry
            ExtractAttributesFromJsonElement(jsonElement, attributes);
        }
        else
        {
            // Try to get Level property via reflection for anonymous objects
            var levelProp = logEntry.GetType().GetProperty("Level");
            level = levelProp?.GetValue(logEntry)?.ToString();

            // Extract semantic attributes from object properties
            ExtractAttributesFromObject(logEntry, attributes);
        }

        // Map severity (OpenTelemetry standard)
        var (severityNumber, severityText) = MapSeverity(level);

        return new
        {
            // OpenTelemetry Log Record fields
            timeUnixNano = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000,
            severityNumber,
            severityText,
            body = logEntry,
            attributes,

            // Trace correlation (if available)
            traceId = Activity.Current?.TraceId.ToString(),
            spanId = Activity.Current?.SpanId.ToString()
        };
    }

    private static void ExtractAttributesFromJsonElement(JsonElement jsonElement, Dictionary<string, object?> attributes)
    {
        // MCP semantic conventions
        if (jsonElement.TryGetProperty("Tool", out var tool))
            attributes["mcp.tool.name"] = tool.GetString();

        if (jsonElement.TryGetProperty("Flow", out var flow))
            attributes["mcp.flow.name"] = flow.GetString();

        if (jsonElement.TryGetProperty("DurationMs", out var duration))
            attributes["mcp.duration_ms"] = duration.GetInt32();

        if (jsonElement.TryGetProperty("Tokens", out var tokens))
            attributes["mcp.tokens"] = tokens.GetInt32();

        if (jsonElement.TryGetProperty("Success", out var success))
            attributes["mcp.success"] = success.GetBoolean();

        // Error semantic conventions (OpenTelemetry standard)
        if (jsonElement.TryGetProperty("ErrorType", out var errorType))
            attributes["error.type"] = errorType.GetString();

        if (jsonElement.TryGetProperty("ErrorMessage", out var errorMessage))
            attributes["error.message"] = errorMessage.GetString();

        if (jsonElement.TryGetProperty("StackTrace", out var stackTrace))
            attributes["error.stack_trace"] = stackTrace.GetString();
    }

    private static void ExtractAttributesFromObject(object logEntry, Dictionary<string, object?> attributes)
    {
        var type = logEntry.GetType();

        // MCP semantic conventions
        var toolProp = type.GetProperty("Tool");
        if (toolProp != null)
            attributes["mcp.tool.name"] = toolProp.GetValue(logEntry)?.ToString();

        var flowProp = type.GetProperty("Flow");
        if (flowProp != null)
            attributes["mcp.flow.name"] = flowProp.GetValue(logEntry)?.ToString();

        var durationProp = type.GetProperty("DurationMs");
        if (durationProp != null)
            attributes["mcp.duration_ms"] = durationProp.GetValue(logEntry);

        var tokensProp = type.GetProperty("Tokens");
        if (tokensProp != null)
            attributes["mcp.tokens"] = tokensProp.GetValue(logEntry);

        var successProp = type.GetProperty("Success");
        if (successProp != null)
            attributes["mcp.success"] = successProp.GetValue(logEntry);

        // Error semantic conventions (OpenTelemetry standard)
        var errorTypeProp = type.GetProperty("ErrorType");
        if (errorTypeProp != null)
            attributes["error.type"] = errorTypeProp.GetValue(logEntry)?.ToString();

        var errorMessageProp = type.GetProperty("ErrorMessage");
        if (errorMessageProp != null)
            attributes["error.message"] = errorMessageProp.GetValue(logEntry)?.ToString();

        var stackTraceProp = type.GetProperty("StackTrace");
        if (stackTraceProp != null)
            attributes["error.stack_trace"] = stackTraceProp.GetValue(logEntry)?.ToString();
    }

    private static (int severityNumber, string severityText) MapSeverity(string? level)
    {
        // OpenTelemetry severity levels: https://opentelemetry.io/docs/specs/otel/logs/data-model/#field-severitynumber
        return level?.ToUpperInvariant() switch
        {
            "TRACE" => (1, "TRACE"),
            "DEBUG" => (5, "DEBUG"),
            "INFORMATION" or "INFO" => (9, "INFO"),
            "WARNING" or "WARN" => (13, "WARN"),
            "ERROR" => (17, "ERROR"),
            "CRITICAL" or "FATAL" => (21, "FATAL"),
            _ => (9, "INFO") // Default to INFO
        };
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _lock?.Dispose();
    }
}
