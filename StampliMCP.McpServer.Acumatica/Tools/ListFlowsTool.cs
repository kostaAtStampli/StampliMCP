using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ListFlowsTool
{
    [McpServerTool(
        Name = "list_flows",
        Title = "List All Flows",
        UseStructuredContent = true
    )]
    [Description("List all integration flows with description and operations count.")]
    public static async Task<CallToolResult> Execute(
        FlowService flowService,
        CancellationToken ct)
    {
        var summaries = new List<FlowSummary>();
        try
        {
            var names = await flowService.GetAllFlowNamesAsync(ct);
            foreach (var name in names)
            {
                var doc = await flowService.GetFlowAsync(name, ct);
                if (doc == null) continue;
                var root = doc.RootElement;
                var description = root.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
                var usedBy = new List<string>();
                if (root.TryGetProperty("usedByOperations", out var used))
                {
                    usedBy = used.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                }
                var summary = new FlowSummary
                {
                    Name = name,
                    Description = string.IsNullOrEmpty(description)
                        ? $"{BuildInfo.Marker}"
                        : $"{description} ({BuildInfo.Marker})",
                    UsedByOperations = usedBy
                };
                summaries.Add(summary);
            }
        }
        catch (Exception ex)
        {
            summaries.Add(new FlowSummary
            {
                Name = "ERROR",
                Description = ex.Message,
                UsedByOperations = new List<string>()
            });
        }

        var ret = new CallToolResult();
        ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = summaries });
        var summaryText = $"flows={summaries.Count} {BuildInfo.Marker}";
        ret.Content.Add(new TextContentBlock { Type = "text", Text = summaryText });
        return ret;
    }
}
