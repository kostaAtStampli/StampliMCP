using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ListPromptsTool
{
    public sealed class PromptInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DeclaringType { get; set; }
    }

    [McpServerTool(Name = "list_prompts", Title = "List Prompts", UseStructuredContent = true)]
    [Description("List server-registered prompts by name and description.")]
    public static CallToolResult Execute()
    {
        var assembly = Assembly.GetExecutingAssembly();
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
        
        // Serialize full prompts list as JSON for LLM consumption
        var jsonOutput = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
        return ret;
    }
}
