using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (MCP protocol requirement)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register services
builder.Services.AddSingleton<KnowledgeService>();
builder.Services.AddSingleton<SearchService>();

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport() // stdio is default for MCP
    .WithToolsFromAssembly(); // Auto-discover all [McpServerTool] methods

await builder.Build().RunAsync();
