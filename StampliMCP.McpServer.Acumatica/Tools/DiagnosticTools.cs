using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class DiagnosticTools
{
    [McpServerTool(Name = "health_check")]
    [Description("Simple health check to verify MCP server is running and responsive")]
    public static Task<object> HealthCheck()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");

        return Task.FromResult<object>(new
        {
            status = "ok",
            smokeTest = "kosta_2025_flow_based",
            version = "2.0.0",
            serverName = "stampli-acumatica",
            timestamp = DateTime.UtcNow.ToString("O"),
            runtime = new
            {
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
            },
            paths = new
            {
                baseDirectory = AppContext.BaseDirectory,
                executablePath = assembly.Location,
                knowledgePath = knowledgePath,
                knowledgeExists = Directory.Exists(knowledgePath),
                workingDirectory = Environment.CurrentDirectory
            }
        });
    }

    [McpServerTool(Name = "check_knowledge_files")]
    [Description("Check which Knowledge files are available as embedded resources")]
    public static Task<object> CheckKnowledgeFiles()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        // Filter to Knowledge resources only
        var knowledgeResources = resourceNames
            .Where(r => r.StartsWith("StampliMCP.McpServer.Acumatica.Knowledge."))
            .Select(r => new
            {
                resourceName = r,
                // Convert StampliMCP.McpServer.Acumatica.Knowledge.categories.json -> categories.json
                // Convert StampliMCP.McpServer.Acumatica.Knowledge.operations.vendors.json -> operations/vendors.json
                fileName = r.Replace("StampliMCP.McpServer.Acumatica.Knowledge.", "")
                            .Replace(".operations.", "operations/")
                            .Replace(".kotlin.", "kotlin/"),
                exists = true,
                size = GetResourceSize(assembly, r)
            })
            .ToList();

        return Task.FromResult<object>(new
        {
            embeddedResources = true,
            totalFiles = knowledgeResources.Count,
            missingFiles = 0,
            files = knowledgeResources
        });
    }

    private static long GetResourceSize(Assembly assembly, string resourceName)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            return stream?.Length ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}