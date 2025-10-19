using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class HealthCheckTool
{
    public sealed class HealthInfo
    {
        public string Status { get; set; } = "ok";
        public string Marker { get; set; } = BuildInfo.Marker;
        public string Version { get; set; } = "4.0.0";
        public string? BuildId { get; set; } = "BUILD_2025_10_18_PROMPT_FIX";
        public string VersionDate { get; set; } = BuildInfo.VersionTag;
        public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");
        public string SdkSpec { get; set; } = "2025-06-18";
    }

    [McpServerTool(Name = "health_check", Title = "Health Check", UseStructuredContent = true)]
    [Description("Returns server health, version, and verification marker.")]
    public static CallToolResult Execute()
    {
        var info = new HealthInfo();

        var result = new CallToolResult();
        result.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = info });
        
        // Serialize full health info as JSON for LLM consumption
        var jsonOutput = System.Text.Json.JsonSerializer.Serialize(info, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        result.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
        return result;
    }
}
