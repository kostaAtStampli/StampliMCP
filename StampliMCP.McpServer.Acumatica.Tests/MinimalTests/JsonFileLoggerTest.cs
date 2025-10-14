using System.Text.Json;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests.MinimalTests;

public class JsonFileLoggerTest
{
    [Fact]
    public async Task JsonFileLogger_Should_Write_To_File()
    {
        // Arrange
        var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
        var logFile = Path.Combine(logDir, "structured.jsonl");

        // Delete file if exists to start fresh
        if (File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        // Act
        using (var logger = new JsonFileLogger())
        {
            await logger.WriteAsync(new
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = "Test",
                Message = "Minimal test"
            });
        }

        // Assert
        Assert.True(File.Exists(logFile), $"Log file should exist at {logFile}");

        var content = await File.ReadAllTextAsync(logFile);
        Assert.False(string.IsNullOrWhiteSpace(content), "Log file should have content");
        Assert.Contains("Minimal test", content);
    }

    [Fact]
    public async Task JsonFileLogger_Should_Write_OpenTelemetry_Format()
    {
        // Arrange
        var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
        var logFile = Path.Combine(logDir, "structured.jsonl");

        if (File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        // Act
        using (var logger = new JsonFileLogger())
        {
            await logger.WriteAsync(new
            {
                Level = "Information",
                Tool = "test_tool",
                Message = "OTel test"
            });
        }

        // Assert - File exists
        Assert.True(File.Exists(logFile), $"Log file should exist at {logFile}");

        // Assert - Parse JSON and check OTel fields
        var content = await File.ReadAllTextAsync(logFile);
        var log = JsonSerializer.Deserialize<JsonElement>(content.Trim());

        // OpenTelemetry required fields
        Assert.True(log.TryGetProperty("timeUnixNano", out var time), "Should have timeUnixNano field");
        Assert.True(time.GetInt64() > 0, "timeUnixNano should be valid Unix timestamp");

        Assert.True(log.TryGetProperty("severityNumber", out var sevNum), "Should have severityNumber field");
        Assert.Equal(9, sevNum.GetInt32()); // INFO = 9

        Assert.True(log.TryGetProperty("severityText", out var sevText), "Should have severityText field");
        Assert.Equal("INFO", sevText.GetString());

        Assert.True(log.TryGetProperty("body", out _), "Should have body field");
    }

    [Fact]
    public async Task JsonFileLogger_Should_Include_Semantic_Attributes()
    {
        // Arrange
        var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
        var logFile = Path.Combine(logDir, "structured.jsonl");

        if (File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        // Act
        using (var logger = new JsonFileLogger())
        {
            await logger.WriteAsync(new
            {
                Level = "Information",
                Tool = "kotlin_tdd_workflow",
                Flow = "VendorExport",
                DurationMs = 1250,
                Tokens = 15000,
                Success = true,
                Message = "Flow completed"
            });
        }

        // Assert
        var content = await File.ReadAllTextAsync(logFile);
        var log = JsonSerializer.Deserialize<JsonElement>(content.Trim());

        // OpenTelemetry semantic attributes should be at root level
        Assert.True(log.TryGetProperty("attributes", out var attrs), "Should have attributes field");

        // MCP semantic conventions
        Assert.True(attrs.TryGetProperty("mcp.tool.name", out var toolName), "Should have mcp.tool.name");
        Assert.Equal("kotlin_tdd_workflow", toolName.GetString());

        Assert.True(attrs.TryGetProperty("mcp.flow.name", out var flowName), "Should have mcp.flow.name");
        Assert.Equal("VendorExport", flowName.GetString());

        Assert.True(attrs.TryGetProperty("mcp.duration_ms", out var duration), "Should have mcp.duration_ms");
        Assert.Equal(1250, duration.GetInt32());

        Assert.True(attrs.TryGetProperty("mcp.tokens", out var tokens), "Should have mcp.tokens");
        Assert.Equal(15000, tokens.GetInt32());

        Assert.True(attrs.TryGetProperty("mcp.success", out var success), "Should have mcp.success");
        Assert.True(success.GetBoolean());
    }

    [Fact]
    public async Task JsonFileLogger_Should_Include_Error_Details()
    {
        // Arrange
        var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
        var logFile = Path.Combine(logDir, "structured.jsonl");

        if (File.Exists(logFile))
        {
            File.Delete(logFile);
        }

        var testException = new InvalidOperationException("Test error");

        // Act
        using (var logger = new JsonFileLogger())
        {
            await logger.WriteAsync(new
            {
                Level = "Error",
                Tool = "kotlin_tdd_workflow",
                Message = "Operation failed",
                ErrorType = testException.GetType().Name,
                ErrorMessage = testException.Message,
                StackTrace = testException.StackTrace
            });
        }

        // Assert
        var content = await File.ReadAllTextAsync(logFile);
        var log = JsonSerializer.Deserialize<JsonElement>(content.Trim());

        // Severity should be ERROR (17)
        Assert.True(log.TryGetProperty("severityNumber", out var sevNum), "Should have severityNumber");
        Assert.Equal(17, sevNum.GetInt32());

        Assert.True(log.TryGetProperty("severityText", out var sevText), "Should have severityText");
        Assert.Equal("ERROR", sevText.GetString());

        // Error attributes should be extracted
        Assert.True(log.TryGetProperty("attributes", out var attrs), "Should have attributes field");

        Assert.True(attrs.TryGetProperty("error.type", out var errorType), "Should have error.type");
        Assert.Equal("InvalidOperationException", errorType.GetString());

        Assert.True(attrs.TryGetProperty("error.message", out var errorMessage), "Should have error.message");
        Assert.Equal("Test error", errorMessage.GetString());

        // Stack trace present (may be null if exception has no stack trace)
        Assert.True(attrs.TryGetProperty("error.stack_trace", out _), "Should have error.stack_trace attribute");
    }
}
