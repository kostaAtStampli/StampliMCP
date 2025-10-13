using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Extensions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class CategoryTools
{
    // Called internally by KotlinTddWorkflowTool
    public static async Task<object> ListCategories(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var categories = await knowledge.GetCategoriesAsync(cancellationToken);
        return new { categories = categories.Select(c => c.ToCategoryResult()) };
    }
}

