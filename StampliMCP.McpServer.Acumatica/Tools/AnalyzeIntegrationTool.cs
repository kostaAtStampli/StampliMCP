using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class AnalyzeIntegrationTool
{
    // Called internally by KotlinTddWorkflowTool
    
    public static async Task<object> AnalyzeIntegrationComplexity(
        
        string featureDescription,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.AnalyzeIntegrationComplexity(featureDescription, ct);
    }
}
