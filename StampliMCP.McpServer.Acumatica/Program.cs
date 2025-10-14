using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Formatting.Compact;
using StampliMCP.McpServer.Acumatica.Services;

// Configure Serilog before creating host
var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs");
Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Information) // MCP protocol: stderr for logs
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logDir, "structured.jsonl"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        buffered: false) // Disable buffering for immediate writes
    .CreateLogger();

try
{
    Log.Information("Starting StampliMCP Acumatica Server");

    var builder = Host.CreateApplicationBuilder(args);

    // Add ServiceDefaults for OpenTelemetry, health checks, and resilience
    builder.AddServiceDefaults();

    // Use Serilog with proper lifecycle management
    builder.Services.AddSerilog(dispose: true); // dispose: true ensures CloseAndFlush on shutdown

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

// Register FlowService for flow-based TDD architecture
builder.Services.AddSingleton<FlowService>();

// Register intelligence service for showcase tools
builder.Services.AddSingleton<IntelligenceService>();

// Register MetricsService for observability
builder.Services.AddSingleton<MetricsService>();

// Register simple JSON file logger (no dependencies, works with PublishSingleFile)
builder.Services.AddSingleton<JsonFileLogger>();

// Register MCP response logger (for test ground truth validation)
builder.Services.AddSingleton<McpResponseLogger>();

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

    Log.Information("StampliMCP Acumatica Server stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "StampliMCP Acumatica Server terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
