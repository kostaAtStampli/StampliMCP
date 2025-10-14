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
}
