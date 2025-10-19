using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ListOperationsTool
{
    public sealed class OperationEntry
    {
        public string Method { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Flow { get; set; }
    }

    [McpServerTool(Name = "list_operations", Title = "List All Operations", UseStructuredContent = true)]
    [Description("List all operations across categories with optional flow mapping.")]
    public static async Task<CallToolResult> Execute(
        KnowledgeService knowledge,
        FlowService flowService,
        CancellationToken ct)
    {
        var results = new List<OperationEntry>();
        var categories = await knowledge.GetCategoriesAsync(ct);
        foreach (var category in categories)
        {
            var ops = await knowledge.GetOperationsByCategoryAsync(category.Name, ct);
            foreach (var op in ops)
            {
                string? flow = await flowService.GetFlowForOperationAsync(op.Method, ct);
                results.Add(new OperationEntry
                {
                    Method = op.Method,
                    Summary = op.Summary,
                    Category = category.Name,
                    Flow = flow
                });
            }
        }
        var ret = new CallToolResult();
        ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = results });
        
        // Serialize full operations list as JSON for LLM consumption
        var jsonOutput = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
        return ret;
    }
}
