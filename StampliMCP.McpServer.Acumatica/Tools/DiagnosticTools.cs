using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class DiagnosticTools
{
    [McpServerTool(Name = "health_check", Title = "Health Check")]
    [Description("Simple health check to verify MCP server is running and responsive")]
    public static Task<object> HealthCheck()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");

        return Task.FromResult<object>(new
        {
            status = "ok",
            smokeTest = "kosta_2025_flow_based",
            version = "4.0.0",
            buildId = "BUILD_2025_10_18_PROMPT_FIX", // UNIQUE MARKER - Proves tokenization + explicit prompt registration (SDK bug workaround)
            kotlinGoldenReference = "TOOL_BASED_2025_10_15", // UNIQUE MARKER - Kotlin via dedicated tool now
            workflow = "get_kotlin_golden_reference_MANDATORY", // Must call get_kotlin_golden_reference before kotlin_tdd_workflow
            toolArchitecture = "NUCLEAR_FIX_FILE_READING_IN_CSHARP", // C# reads Kotlin files, not Claude scanning
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

    [McpServerTool(Name = "check_knowledge_files", Title = "Check Knowledge Files")]
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