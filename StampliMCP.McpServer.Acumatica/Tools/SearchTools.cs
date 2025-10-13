using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class SearchTools
{
    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> SearchOperations(
        SearchService search,
        string query,
        CancellationToken cancellationToken)
    {
        var results = await search.SearchAsync(query, cancellationToken);
        return new { matches = results.Select(r => new { operation = r.Operation, match = r.Match }) };
    }
}

