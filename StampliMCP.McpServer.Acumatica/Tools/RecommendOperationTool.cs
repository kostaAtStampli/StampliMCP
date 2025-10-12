using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class RecommendOperationTool
{
    [McpServerTool(Name = "recommend_operation")]
    [Description("Takes a business requirement in plain English and recommends the best Acumatica operation to use, with alternatives, trade-offs, implementation approach, and business considerations. Bridges the gap between business and technical requirements.")]
    public static async Task<object> RecommendOperation(
        [Description("Business requirement in natural language (e.g., 'pay multiple vendors at once from a single approval')")]
        string businessRequirement,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.RecommendOperation(businessRequirement, ct);
    }
}
