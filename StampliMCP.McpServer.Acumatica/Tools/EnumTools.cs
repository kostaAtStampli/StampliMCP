using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class EnumTools
{
    [McpServerTool(Name = "get_enums")]
    [Description("Get all enum mappings (VendorStatus, ItemType, etc.) with code locations")]
    public static async Task<object> GetEnums(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var enums = await knowledge.GetEnumsAsync(cancellationToken);
        return new { enums };
    }

    [McpServerTool(Name = "get_test_config")]
    [Description("Get test customer configuration and golden test examples")]
    public static async Task<object> GetTestConfig(
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var config = await knowledge.GetTestConfigAsync(cancellationToken);
        return config;
    }

    [McpServerTool(Name = "get_base_classes")]
    [Description("Get base request/response class information that all DTOs inherit from")]
    public static async Task<object> GetBaseClasses(CancellationToken cancellationToken)
    {
        var baseClassesPath = Path.Combine(AppContext.BaseDirectory, "Knowledge", "base-classes.json");
        var json = await File.ReadAllTextAsync(baseClassesPath, cancellationToken);
        return JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        }) ?? new { };
    }
}

