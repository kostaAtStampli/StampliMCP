using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class SearchTools
{
    [McpServerTool(Name = "search_operations")]
    [Description("Search for operations by keyword in method names or summaries")]
    public static async Task<object> SearchOperations(
        SearchService search,
        [Description("Search keyword (e.g., 'duplicate', 'vendor', 'export')")] string query,
        CancellationToken cancellationToken)
    {
        var results = await search.SearchAsync(query, cancellationToken);
        return new { matches = results.Select(r => new { operation = r.Operation, match = r.Match }) };
    }
}

