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
        var tempPath = logDir ?? Path.GetTempPath();
        var logFiles = Directory.GetFiles(tempPath, "mcp_responses_*.jsonl");

        if (logFiles.Length == 0)
        {
            throw new FileNotFoundException("No MCP response logs found. MCP tool may not have been called.");
        }

        var latestLog = logFiles.OrderByDescending(File.GetLastWriteTime).First();
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
            OperationCount = logEntry.OperationCount,
            ResponseSize = logEntry.ResponseSize,
            Timestamp = DateTime.Parse(logEntry.Timestamp),
            KnowledgeFiles = ExtractKnowledgeFiles(logEntry.Response),
            Operations = ExtractOperations(logEntry.Response),
            RawResponse = logEntry.Response
        };
    }

    private static List<KnowledgeFile> ExtractKnowledgeFiles(McpResponse response)
    {
        var files = new List<KnowledgeFile>();

        // Extract from supportingKnowledge array
        if (response.SupportingKnowledge != null)
        {
            foreach (var item in response.SupportingKnowledge)
            {
                files.Add(new KnowledgeFile
                {
                    File = item.File,
                    Description = item.Description
                });
            }
        }

        return files;
    }

    private static List<string> ExtractOperations(McpResponse response)
    {
        var operations = new List<string>();

        if (response.AvailableOperations != null)
        {
            foreach (var op in response.AvailableOperations)
            {
                if (op.TryGetProperty("method", out var method))
                {
                    operations.Add(method.GetString() ?? "");
                }
            }
        }

        return operations.Where(o => !string.IsNullOrEmpty(o)).ToList();
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
    public required int OperationCount { get; init; }
    public required int ResponseSize { get; init; }
    public required DateTime Timestamp { get; init; }
    public required List<KnowledgeFile> KnowledgeFiles { get; init; }
    public required List<string> Operations { get; init; }
    public required McpResponse RawResponse { get; init; }
}

public record KnowledgeFile
{
    public required string File { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// MCP log entry structure (matches KotlinTddWorkflowTool.cs logging format)
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

    [JsonPropertyName("responseSize")]
    public required int ResponseSize { get; init; }

    [JsonPropertyName("operationCount")]
    public required int OperationCount { get; init; }

    [JsonPropertyName("response")]
    public required McpResponse Response { get; init; }
}

public record McpResponse
{
    [JsonPropertyName("userRequest")]
    public string? UserRequest { get; init; }

    [JsonPropertyName("yourTask")]
    public string? YourTask { get; init; }

    [JsonPropertyName("availableOperations")]
    public JsonElement[]? AvailableOperations { get; init; }

    [JsonPropertyName("supportingKnowledge")]
    public SupportingKnowledgeItem[]? SupportingKnowledge { get; init; }

    [JsonPropertyName("projectPaths")]
    public ProjectPaths? ProjectPaths { get; init; }
}

public record SupportingKnowledgeItem
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public record ProjectPaths
{
    [JsonPropertyName("legacyRoot")]
    public string? LegacyRoot { get; init; }

    [JsonPropertyName("moduleRoot")]
    public string? ModuleRoot { get; init; }

    [JsonPropertyName("testFile")]
    public string? TestFile { get; init; }

    [JsonPropertyName("implFile")]
    public string? ImplFile { get; init; }
}
