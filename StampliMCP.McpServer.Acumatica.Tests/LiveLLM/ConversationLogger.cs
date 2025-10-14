using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Logs complete conversations between LLM and MCP for analysis
/// </summary>
public sealed class ConversationLogger
{
    private readonly string _logDir;
    private readonly string _testName;
    private readonly List<ConversationTurn> _turns = new();
    private readonly DateTime _startTime;

    public ConversationLogger(string testName, string? logDir = null)
    {
        _testName = testName;
        _startTime = DateTime.UtcNow;
        _logDir = logDir ?? Path.Combine(Directory.GetCurrentDirectory(), "LiveLLM", "Logs");
        Directory.CreateDirectory(_logDir);
    }

    /// <summary>
    /// Log an LLM prompt/response
    /// </summary>
    public void LogLLMTurn(string prompt, string response, List<ToolCall>? toolCalls = null)
    {
        _turns.Add(new ConversationTurn
        {
            TurnNumber = _turns.Count + 1,
            Timestamp = DateTime.UtcNow,
            Actor = "LLM",
            Content = new TurnContent
            {
                Prompt = prompt,
                Response = response,
                ToolCalls = toolCalls
            }
        });
    }

    /// <summary>
    /// Log an MCP tool call and response
    /// </summary>
    public void LogMCPTurn(string toolName, object arguments, object response)
    {
        _turns.Add(new ConversationTurn
        {
            TurnNumber = _turns.Count + 1,
            Timestamp = DateTime.UtcNow,
            Actor = "MCP",
            Content = new TurnContent
            {
                ToolName = toolName,
                ToolArguments = JsonSerializer.Serialize(arguments),
                ToolResponse = JsonSerializer.Serialize(response)
            }
        });
    }

    /// <summary>
    /// Log a file operation (write, read, etc.)
    /// </summary>
    public void LogFileOperation(string operation, string filePath, string? content = null)
    {
        _turns.Add(new ConversationTurn
        {
            TurnNumber = _turns.Count + 1,
            Timestamp = DateTime.UtcNow,
            Actor = "FileSystem",
            Content = new TurnContent
            {
                FileOperation = operation,
                FilePath = filePath,
                FileContent = content
            }
        });
    }

    /// <summary>
    /// Log an MCP tool call
    /// </summary>
    public void LogToolCall(string toolName, string arguments)
    {
        // Add a new turn with tool call
        _turns.Add(new ConversationTurn
        {
            TurnNumber = _turns.Count + 1,
            Timestamp = DateTime.UtcNow,
            Actor = "MCP",
            Content = new TurnContent
            {
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Name = toolName,
                        Arguments = arguments
                    }
                }
            }
        });
    }

    /// <summary>
    /// Log complete MCP tool response with all instructions (CRITICAL for debugging)
    /// </summary>
    public void LogMCPResponse(string toolName, string responseJson)
    {
        _turns.Add(new ConversationTurn
        {
            TurnNumber = _turns.Count + 1,
            Timestamp = DateTime.UtcNow,
            Actor = "MCP_RESPONSE",
            Content = new TurnContent
            {
                ToolName = toolName,
                ToolResponse = responseJson
            }
        });
    }

    /// <summary>
    /// Check if any tool call was made
    /// </summary>
    public bool HasToolCall(string toolName)
    {
        return _turns.Any(t =>
            t.Content.ToolCalls?.Any(tc => tc.Name.Contains(toolName)) == true);
    }

    /// <summary>
    /// Get all tool calls
    /// </summary>
    public List<ToolCall> GetAllToolCalls()
    {
        return _turns
            .SelectMany(t => t.Content.ToolCalls ?? new List<ToolCall>())
            .ToList();
    }

    /// <summary>
    /// Save conversation to JSON file
    /// </summary>
    public string SaveConversation(bool success, string? errorMessage = null)
    {
        var endTime = DateTime.UtcNow;
        var duration = endTime - _startTime;

        var conversation = new
        {
            TestName = _testName,
            Success = success,
            Error = errorMessage,
            StartTime = _startTime,
            EndTime = endTime,
            DurationSeconds = duration.TotalSeconds,
            TotalTurns = _turns.Count,
            Turns = _turns
        };

        // Add GUID for uniqueness to avoid conflicts in parallel test runs
        var fileName = $"{_testName}_{_startTime:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}_{(success ? "success" : "failed")}.json";
        var filePath = Path.Combine(_logDir, fileName);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(conversation, options));

        // If successful, also save to GoldenFlows
        if (success)
        {
            var goldenDir = Path.Combine(Directory.GetCurrentDirectory(), "LiveLLM", "GoldenFlows");
            Directory.CreateDirectory(goldenDir);
            var goldenPath = Path.Combine(goldenDir, $"{_testName}_golden.json");
            File.WriteAllText(goldenPath, JsonSerializer.Serialize(conversation, options));
        }

        return filePath;
    }

    public IReadOnlyList<ConversationTurn> Turns => _turns.AsReadOnly();
}

public record ConversationTurn
{
    public int TurnNumber { get; init; }
    public DateTime Timestamp { get; init; }
    public string Actor { get; init; } = string.Empty;
    public TurnContent Content { get; init; } = new();
}

public record TurnContent
{
    public string? Prompt { get; init; }
    public string? Response { get; init; }
    public List<ToolCall>? ToolCalls { get; init; }
    public string? ToolName { get; init; }
    public string? ToolArguments { get; init; }
    public string? ToolResponse { get; init; }
    public string? FileOperation { get; init; }
    public string? FilePath { get; init; }
    public string? FileContent { get; init; }
}

public record ToolCall
{
    public string Name { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
}
