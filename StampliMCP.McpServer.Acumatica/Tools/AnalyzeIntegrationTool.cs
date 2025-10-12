using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class AnalyzeIntegrationTool
{
    [McpServerTool(Name = "analyze_integration_complexity")]
    [Description("Analyzes a feature description and provides intelligent complexity assessment, effort estimates, required operations, risks, and dependencies. Like having a senior architect plan your integration.")]
    public static async Task<object> AnalyzeIntegrationComplexity(
        [Description("Natural language description of the feature to implement (e.g., 'vendor onboarding with duplicate checks and email notifications')")]
        string featureDescription,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.AnalyzeIntegrationComplexity(featureDescription, ct);
    }
}
