using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class RecommendOperationTool
{
    // Called internally by KotlinTddWorkflowTool
    
    internal static async Task<object> RecommendOperation(
        
        string businessRequirement,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.RecommendOperation(businessRequirement, ct);
    }
}
