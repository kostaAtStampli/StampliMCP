using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults for OpenTelemetry, health checks, and resilience
builder.AddServiceDefaults();

// Configure logging to stderr (MCP protocol requirement)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add memory cache for performance
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 100; // Limit cache entries
    options.CompactionPercentage = 0.25;
});

// Register services with keyed service pattern (new in .NET 8+)
builder.Services.AddKeyedSingleton<KnowledgeService>("knowledge");
builder.Services.AddKeyedSingleton<SearchService>("search");

// Register non-keyed services for backward compatibility
builder.Services.AddSingleton<KnowledgeService>(sp => sp.GetRequiredKeyedService<KnowledgeService>("knowledge"));
builder.Services.AddSingleton<SearchService>(sp => sp.GetRequiredKeyedService<SearchService>("search"));

// Configure MCP server with latest features
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "stampli-acumatica",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport() // stdio is default for MCP
    .WithToolsFromAssembly(); // Auto-discover all [McpServerTool] methods

var app = builder.Build();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("StampliMCP Acumatica Server starting with MCP protocol 2025-06-18");
logger.LogInformation("OpenTelemetry and health checks configured via ServiceDefaults");

await app.RunAsync();
