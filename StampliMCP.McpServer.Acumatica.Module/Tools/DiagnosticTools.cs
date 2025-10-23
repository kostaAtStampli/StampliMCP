using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class DiagnosticTools
{
[McpServerTool(Name = "health_check_legacy", Title = "Legacy Health Check")]
[Description("Simple health check to verify MCP server is running and responsive")]
public static object HealthCheck()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");

        return new
        {
            status = "ok",
            smokeTest = "kosta_2025_flow_based",
            version = "4.0.0",
            buildId = "BUILD_2025_10_18_PROMPT_FIX", // UNIQUE MARKER - Proves tokenization + explicit prompt registration (SDK bug workaround)
            kotlinGoldenReference = "TOOL_BASED_2025_10_15", // UNIQUE MARKER - Kotlin via dedicated tool now
            workflow = "get_kotlin_golden_reference_MANDATORY", // Must call get_kotlin_golden_reference before kotlin_tdd_workflow
            toolArchitecture = "NUCLEAR_FIX_FILE_READING_IN_CSHARP", // C# reads Kotlin files, not Claude scanning
            serverName = assembly.GetName().Name,
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
                knowledgePath = knowledgePath,
                knowledgeExists = Directory.Exists(knowledgePath),
                workingDirectory = Environment.CurrentDirectory
            },
            nextActions = new[]
            {
                new ResourceLinkBlock
                {
                    Uri = "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=vendor",
                    Name = "Query knowledge base",
                    Description = "Search for operations and flows"
                },
                new ResourceLinkBlock
                {
                    Uri = "mcp://stampli-unified/check_knowledge_files",
                    Name = "List knowledge files",
                    Description = "View all embedded knowledge resources"
                }
            }
        };
    }

    [McpServerTool(Name = "check_knowledge_files", Title = "Check Knowledge Files")]
    [Description("Check which Knowledge files are available as embedded resources")]
    public static object CheckKnowledgeFiles()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        // Filter to Knowledge resources only
        var knowledgeResources = resourceNames
            .Where(r => r.StartsWith("StampliMCP.McpServer.Acumatica.Module.Knowledge."))
            .Select(r => new
            {
                resourceName = r,
                // Convert StampliMCP.McpServer.Acumatica.Knowledge.categories.json -> categories.json
                // Convert StampliMCP.McpServer.Acumatica.Knowledge.operations.vendors.json -> operations/vendors.json
                fileName = r.Replace("StampliMCP.McpServer.Acumatica.Module.Knowledge.", "")
                            .Replace(".operations.", "operations/")
                            .Replace(".kotlin.", "kotlin/"),
                exists = true,
                size = GetResourceSize(assembly, r)
            })
            .ToList();

        return new
        {
            embeddedResources = true,
            totalFiles = knowledgeResources.Count,
            missingFiles = 0,
            files = knowledgeResources
        };
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
