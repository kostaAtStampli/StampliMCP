#if DEV_TOOLS
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using StampliMCP.McpServer.Unified.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ElicitationDebugTool
{
    [McpServerTool(
        Name = "mcp__debug_elicitation",
        Title = "Elicitation Capability Probe",
        UseStructuredContent = true)]
    [Description("Check whether the connected MCP client supports elicitation prompts.")]
    public static async Task<CallToolResult> Execute(IMcpServer server, CancellationToken ct)
    {
        var fields = new[]
        {
            new ElicitationCompat.Field(
                Name: "confirm",
                Kind: "boolean",
                Description: "Set to true if you see this prompt (confirms elicitation support)")
        };

        var outcome = await ElicitationCompat.TryElicitAsync(
            server,
            "Testing elicitation support. Toggle the checkbox (accept) to confirm support, or cancel to decline.",
            fields,
            ct);

        if (outcome.Supported)
        {
            Log.Debug("Debug elicitation action={Action}", outcome.Action ?? "none");
        }

        var contentSnapshot = outcome.Content?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ValueKind == JsonValueKind.Undefined ? kvp.Value.GetRawText() : kvp.Value.ToString());

        var payload = new
        {
            supported = outcome.Supported,
            action = outcome.Action ?? "none",
            accepted = string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase),
            content = contentSnapshot
        };

        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result = payload })
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        result.Content.Add(new TextContentBlock { Type = "text", Text = json });

        return result;
    }
}

#endif
