using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Extensions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class CategoryTools
{
    [McpServerTool(Name = "list_categories")]
    [Description("List all available operation categories in Acumatica integration")]
    public static async Task<object> ListCategories(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var categories = await knowledge.GetCategoriesAsync(cancellationToken);
        return new { categories = categories.Select(c => c.ToCategoryResult()) };
    }
}

