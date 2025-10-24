#if DEV_TOOLS
using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpPromptTools
{
    public sealed class PromptInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DeclaringType { get; set; }
    }

    [McpServerTool(Name = "erp__list_prompts", Title = "List ERP Prompts", UseStructuredContent = true)]
    [Description("List server-registered prompts for a specific ERP module by name and description.")]
    public static CallToolResult Execute([Description("ERP identifier (e.g., acumatica)")] string erp, ErpRegistry registry)
    {
        using var facade = registry.GetFacade(erp);
        var assembly = facade.Knowledge.GetType().Assembly;

        var results = new List<PromptInfo>();

        foreach (var type in assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerPromptTypeAttribute>() is null) continue;
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = m.GetCustomAttribute<McpServerPromptAttribute>();
                if (attr is null) continue;
                var desc = m.GetCustomAttribute<DescriptionAttribute>()?.Description;
                results.Add(new PromptInfo { Name = attr.Name ?? m.Name, Description = desc, DeclaringType = type.FullName });
            }
        }

        var ret = new CallToolResult();
        ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = results });
        var jsonOutput = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
        return ret;
    }
}


#endif
