using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults for OpenTelemetry, health checks, and resilience
builder.AddServiceDefaults();

// Configure logging for MCP - MUST redirect all output to stderr
// MCP requires clean stdout for JSON-RPC communication only
builder.Logging.ClearProviders(); // Remove all default loggers first

// Add console logging but redirect ALL output to stderr
// This is the critical configuration per MCP specification
builder.Logging.AddConsole(options =>
{
    // Send ALL log levels to stderr, keeping stdout clean for JSON-RPC
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Set minimum log level based on environment
var debugMode = Environment.GetEnvironmentVariable("MCP_DEBUG") == "true";
builder.Logging.SetMinimumLevel(debugMode ? LogLevel.Debug : LogLevel.Warning);

// Suppress all console output from the host itself
builder.Services.Configure<ConsoleLifetimeOptions>(options =>
{
    options.SuppressStatusMessages = true;
});

// Ensure host doesn't write startup messages
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(5);
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

// Configure MCP server with latest features
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "stampli-acumatica",
            Version = "2.0.0" // Updated for prompts feature
        };
    })
    .WithStdioServerTransport() // stdio is default for MCP
    .WithPromptsFromAssembly(typeof(Program).Assembly) // Explicitly pass assembly for Native AOT compatibility
    .WithToolsFromAssembly(typeof(Program).Assembly); // Explicitly pass assembly for Native AOT compatibility

var app = builder.Build();

// Startup logging disabled for MCP stdio compatibility
// The server will communicate via JSON-RPC over stdio, not console logs

await app.RunAsync();
