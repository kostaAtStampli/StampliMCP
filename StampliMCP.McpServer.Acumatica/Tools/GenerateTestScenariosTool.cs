using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class GenerateTestScenariosTool
{
    [McpServerTool(Name = "generate_test_scenarios")]
    [Description("Generates comprehensive test scenarios for an operation: happy path, edge cases, error cases, performance tests, and security tests. Like having a QA engineer design your test plan.")]
    public static async Task<object> GenerateTestScenarios(
        [Description("Operation name to generate test scenarios for (e.g., 'exportVendor', 'exportBillPayment')")]
        string operationName,
        IntelligenceService intelligence,
        CancellationToken ct = default)
    {
        return await intelligence.GenerateTestScenarios(operationName, ct);
    }
}
