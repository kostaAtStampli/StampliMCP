using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpKnowledgeFilesTool
{
    [McpServerTool(Name = "erp__check_knowledge_files", Title = "Check ERP Knowledge Files", UseStructuredContent = true)]
    [Description("List embedded Knowledge resources for the given ERP module.")]
    public static CallToolResult Execute([Description("ERP identifier (e.g., acumatica)")] string erp, ErpRegistry registry)
    {
        using var facade = registry.GetFacade(erp);
        var assembly = facade.Knowledge.GetType().Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var results = new List<object>();
        foreach (var r in resourceNames)
        {
            if (!r.Contains(".Knowledge.")) continue;

            string fileName = r;
            try
            {
                // Try to trim the assembly prefix leading to Knowledge folder
                var idx = r.IndexOf(".Knowledge.", StringComparison.Ordinal);
                if (idx > 0)
                {
                    fileName = r[(idx + ".Knowledge.".Length)..]
                        .Replace(".operations.", "operations/")
                        .Replace(".kotlin.", "kotlin/");
                }
            }
            catch
            {
                // leave fileName as is
            }

            results.Add(new
            {
                resourceName = r,
                fileName
            });
        }

        var payload = new
        {
            erp = erp,
            totalFiles = results.Count,
            files = results
        };

        var ret = new CallToolResult
        {
            StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = payload })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        ret.Content.Add(new TextContentBlock { Type = "text", Text = json });
        return ret;
    }
}

