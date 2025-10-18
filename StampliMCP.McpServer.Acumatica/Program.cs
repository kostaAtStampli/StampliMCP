using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Formatting.Compact;
using StampliMCP.McpServer.Acumatica.Prompts;
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

// Configure MCP server with composable knowledge architecture
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "stampli-acumatica",
            Version = "4.0.0-BUILD_2025_10_18_PROMPT_FIX" // Protocol 2025-06-18: Composable tools, elicitation, structured outputs + tokenization fix + EXPLICIT PROMPT REGISTRATION (SDK 0.4.0-preview.2 bug workaround)
        };

        // Note: Elicitation is a CLIENT capability, not server
        // Servers request elicitation via McpServer.ElicitAsync<T>()
    })
    .WithStdioServerTransport() // stdio is default for MCP
    .WithToolsFromAssembly() // Auto-discover tools
    // WORKAROUND: WithPromptsFromAssembly() broken in 0.4.0-preview.2 (fixed in later versions)
    // GitHub PR "Fixing WithPromptsFromAssembly" merged after 0.4.0-preview.2
    // Using explicit .WithPrompts<T>() registration with NON-STATIC classes
    // Made all prompt classes sealed (non-static) so generic overload works
    .WithPrompts<KotlinTddTasklistPrompt>()
    .WithPrompts<TestPlanningPrompt>()
    .WithPrompts<TroubleshootingPrompt>()
    .WithPrompts<AnalyzeIntegrationPrompt>()
    .WithPrompts<KotlinFeaturePrompt>()
    .WithResourcesFromAssembly(); // Auto-discover MCP resources

var app = builder.Build();

    // Log startup information
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("StampliMCP Acumatica Server v4.0.0 - Protocol 2025-06-18 Composable Architecture");
    logger.LogInformation("Composable Tools (9): query_acumatica_knowledge, get_flow_details, recommend_flow, validate_request, diagnose_error, get_kotlin_golden_reference, kotlin_tdd_workflow, health_check, check_knowledge_files");
    logger.LogInformation("Features: Structured outputs, Elicitation (interactive refinement), Tool chaining (resource links)");
    logger.LogInformation("Knowledge: 48 operations, 9 flows, Kotlin golden reference (exportVendor)");

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
