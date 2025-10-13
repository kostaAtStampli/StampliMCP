using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class EnumTools
{
    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> GetEnums(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var enums = await knowledge.GetEnumsAsync(cancellationToken);
        return new { enums };
    }

    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> GetTestConfig(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var config = await knowledge.GetTestConfigAsync(cancellationToken);
        return config;
    }

    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> GetBaseClasses(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        return await knowledge.GetBaseClassesAsync(cancellationToken);
    }
}

