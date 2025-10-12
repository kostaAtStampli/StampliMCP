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
    [Description("Check which Knowledge files are available and accessible")]
    public static Task<object> CheckKnowledgeFiles()
    {
        var knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");
        var files = new List<object>();

        // Check main knowledge files
        var mainFiles = new[]
        {
            "categories.json",
            "enums.json",
            "error-catalog.json",
            "base-classes.json",
            "test-config.json",
            "integration-strategy.json",
            "method-signatures.json",
            "reflection-mechanism.json"
        };

        foreach (var file in mainFiles)
        {
            var filePath = Path.Combine(knowledgePath, file);
            files.Add(new
            {
                file = file,
                path = filePath,
                exists = File.Exists(filePath),
                size = File.Exists(filePath) ? new FileInfo(filePath).Length : 0
            });
        }

        // Check operations folder
        var operationsPath = Path.Combine(knowledgePath, "operations");
        if (Directory.Exists(operationsPath))
        {
            var operationFiles = Directory.GetFiles(operationsPath, "*.json");
            foreach (var opFile in operationFiles)
            {
                var fileName = Path.GetFileName(opFile);
                files.Add(new
                {
                    file = $"operations/{fileName}",
                    path = opFile,
                    exists = true,
                    size = new FileInfo(opFile).Length
                });
            }
        }

        // Check kotlin folder
        var kotlinPath = Path.Combine(knowledgePath, "kotlin");
        if (Directory.Exists(kotlinPath))
        {
            var kotlinFiles = Directory.GetFiles(kotlinPath);
            foreach (var kotlinFile in kotlinFiles)
            {
                var fileName = Path.GetFileName(kotlinFile);
                files.Add(new
                {
                    file = $"kotlin/{fileName}",
                    path = kotlinFile,
                    exists = true,
                    size = new FileInfo(kotlinFile).Length
                });
            }
        }

        return Task.FromResult<object>(new
        {
            knowledgePath = knowledgePath,
            knowledgeDirectoryExists = Directory.Exists(knowledgePath),
            totalFiles = files.Count(f => ((dynamic)f).exists),
            missingFiles = files.Count(f => !((dynamic)f).exists),
            files = files
        });
    }
}