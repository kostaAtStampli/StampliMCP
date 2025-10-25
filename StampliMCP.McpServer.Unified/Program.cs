using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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
        .WithListResourcesHandler(ListResourcesAsync)
        .WithReadResourceHandler(ReadResourceAsync)
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

static ValueTask<ListResourcesResult> ListResourcesAsync(RequestContext<ListResourcesRequestParams> context, CancellationToken cancellationToken)
{
    var result = new ListResourcesResult
    {
        Resources =
        [
            new Resource
            {
                Uri = "mcp://stampli-unified/help/tool-link",
                Name = "How to run tool links",
                Description = "Open to see how to execute mcp:// tool links via tools/call"
            }
        ]
    };

    return ValueTask.FromResult(result);
}

static ValueTask<ReadResourceResult> ReadResourceAsync(RequestContext<ReadResourceRequestParams> context, CancellationToken cancellationToken)
{
    var uri = context.Params?.Uri ?? string.Empty;
    if (!uri.StartsWith("mcp://stampli-unified/", StringComparison.OrdinalIgnoreCase))
    {
        throw new McpProtocolException($"Unknown resource: {uri}", null, McpErrorCode.MethodNotFound);
    }

    var parsed = new Uri(uri.Replace("mcp://", "http://"));
    var toolName = parsed.AbsolutePath.Trim('/');
    var query = HttpUtility.ParseQueryString(parsed.Query);
    var arguments = query.AllKeys?.Where(k => k is not null)
        .ToDictionary(k => k!, k => (object?)query[k], StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    var payload = new
    {
        note = "This is a tool link. Use tools/call to execute.",
        example = new { method = "tools/call", name = toolName, arguments }
    };

    var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });

    var result = new ReadResourceResult
    {
        Contents =
        [
            new TextResourceContents
            {
                MimeType = "application/json",
                Text = json
            }
        ]
    };

    return ValueTask.FromResult(result);
}

static bool TryParseToolLink(string uri, out string toolName, out IDictionary<string, object?> arguments)
{
    const string prefix = "mcp://stampli-unified/";
    toolName = string.Empty;
    arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var remainder = uri[prefix.Length..];
    if (string.IsNullOrWhiteSpace(remainder))
    {
        return false;
    }

    var parts = remainder.Split('?', 2);
    toolName = NormalizeToolName(parts[0].Trim('/'));
    if (string.IsNullOrWhiteSpace(toolName))
    {
        return false;
    }

    if (parts.Length == 2 && parts[1].Length > 0)
    {
        foreach (var pair in parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            arguments[key] = value;
        }
    }

    return true;
}

static string NormalizeToolName(string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return raw;
    }

    return raw switch
    {
        "erpquery_knowledge" => "erp__query_knowledge",
        "erprecommend_flow" => "erp__recommend_flow",
        "erpvalidate_request" => "erp__validate_request",
        "erpdiagnose_error" => "erp__diagnose_error",
        "erphealth_check" => "erp__health_check",
        "erpknowledge_update_plan" => "erp__knowledge_update_plan",
        _ => raw
    };
}
