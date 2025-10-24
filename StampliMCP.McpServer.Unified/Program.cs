using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Formatting.Compact;
using StampliMCP.McpServer.Acumatica.Module;
using StampliMCP.McpServer.Acumatica.Prompts;
using StampliMCP.McpServer.Intacct.Module;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;

var logDir = Path.Combine(Path.GetTempPath(), "mcp_logs", "unified");
Directory.CreateDirectory(logDir);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Information)
    .WriteTo.File(
        new CompactJsonFormatter(),
        Path.Combine(logDir, "structured.jsonl"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        buffered: false)
    .CreateLogger();

try
{
    Log.Information("Starting StampliMCP Unified Server");

    var builder = Host.CreateApplicationBuilder(args);

    builder.AddServiceDefaults();
    builder.Services.AddSerilog(dispose: true);

    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 2000;
        options.CompactionPercentage = 0.20;
        options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
    });

    // Register ERP modules
    IErpModule[] modules =
    {
        new AcumaticaModule(),
        new IntacctModule()
    };

    foreach (var module in modules)
    {
        builder.Services.AddSingleton<IErpModule>(module);
        module.RegisterServices(builder.Services);
    }

    builder.Services.AddSingleton<ErpRegistry>();
    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "stampli-unified",
                Version = "1.0.0-alpha"
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly()
        .WithToolsFromAssembly(typeof(AcumaticaModule).Assembly)
        .WithToolsFromAssembly(typeof(IntacctModule).Assembly)
        // WithPromptsFromAssembly is unstable in 0.4.0-preview.2, register explicitly
        .WithPrompts<KotlinTddTasklistPrompt>()
        .WithPrompts<TestPlanningPrompt>()
        .WithPrompts<TroubleshootingPrompt>()
        .WithPrompts<AnalyzeIntegrationPrompt>()
        .WithPrompts<KotlinFeaturePrompt>()
        .WithPrompts<TwoScanEnforcementPrompt>()
        .WithResourcesFromAssembly()
        .WithResourcesFromAssembly(typeof(AcumaticaModule).Assembly)
        .WithResourcesFromAssembly(typeof(IntacctModule).Assembly);

    var app = builder.Build();

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var registry = app.Services.GetRequiredService<ErpRegistry>();
    var descriptors = string.Join(", ", registry.ListErps().Select(d => d.Key));
    logger.LogInformation("Unified MCP ready. Registered ERPs: {Erps}", descriptors);

    await app.RunAsync();

    Log.Information("StampliMCP Unified Server stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "StampliMCP Unified Server terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
