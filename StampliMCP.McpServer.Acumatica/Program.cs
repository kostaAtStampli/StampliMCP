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

// Register intelligence service for showcase tools
builder.Services.AddSingleton<IntelligenceService>();

// Configure MCP server with single entry point architecture
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "stampli-acumatica",
            Version = "3.0.0" // Major version for single entry point architecture
        };
    })
    .WithStdioServerTransport() // stdio is default for MCP
    .WithToolsFromAssembly(); // Still auto-discover but only decorated tools will be found

var app = builder.Build();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("StampliMCP Acumatica Server v3.0.0 starting - Single Entry Point Architecture");
logger.LogInformation("Main Tool: kotlin_tdd_workflow (start, continue, query, list)");
logger.LogInformation("Diagnostic: health_check for system verification");
logger.LogInformation("All other tools are now internal helpers, not exposed to MCP");

await app.RunAsync();
