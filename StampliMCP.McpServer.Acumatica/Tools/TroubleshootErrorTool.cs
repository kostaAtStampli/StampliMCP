using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class TroubleshootErrorTool
{
    [McpServerTool(Name = "troubleshoot_error")]
    [Description("Takes an error message and provides intelligent troubleshooting: root cause analysis, immediate fix steps, prevention strategies, and related errors. Like having a senior developer debug for you.")]
    public static async Task<object> TroubleshootError(
        [Description("Error message from production or testing (e.g., 'vendorName exceeds maximum length of 60 characters')")]
        string errorMessage,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.TroubleshootError(errorMessage, ct);
    }
}
