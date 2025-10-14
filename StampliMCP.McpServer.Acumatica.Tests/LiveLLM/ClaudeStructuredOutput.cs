using System.Text.Json;
using System.Text.Json.Serialization;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Models for Claude's structured JSON output (2025 AI-first verification)
/// </summary>
public record ClaudeStructuredOutput
{
    [JsonPropertyName("operationSelected")]
    public required string OperationSelected { get; init; }

    [JsonPropertyName("reasoning")]
    public required string Reasoning { get; init; }

    [JsonPropertyName("filesScanned")]
    public required List<FileScanned> FilesScanned { get; init; }

    [JsonPropertyName("tasklistSummary")]
    public required string TasklistSummary { get; init; }

    /// <summary>
    /// Parse Claude's output, handling potential markdown code blocks
    /// </summary>
    public static ClaudeStructuredOutput Parse(string output)
    {
        // Remove markdown code blocks if present
        var json = output.Trim();
        if (json.StartsWith("```json"))
        {
            json = json.Replace("```json", "").Replace("```", "").Trim();
        }
        else if (json.StartsWith("```"))
        {
            json = json.Replace("```", "").Trim();
        }

        try
        {
            var result = JsonSerializer.Deserialize<ClaudeStructuredOutput>(json);
            if (result == null)
            {
                throw new InvalidOperationException("Deserialization returned null");
            }
            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse Claude's JSON output. Expected structured JSON but got:\n{output.Substring(0, Math.Min(500, output.Length))}...",
                ex);
        }
    }
}

public record FileScanned
{
    [JsonPropertyName("file")]
    public required string File { get; init; }

    [JsonPropertyName("lines")]
    public required string Lines { get; init; }

    [JsonPropertyName("keyPattern")]
    public required string KeyPattern { get; init; }

    /// <summary>
    /// Parse line range like "35-72" into (start, end)
    /// </summary>
    public (int Start, int End) ParseLineRange()
    {
        var parts = Lines.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end))
        {
            throw new FormatException($"Invalid line range format: {Lines}. Expected 'X-Y'");
        }
        return (start, end);
    }
}
