using System;
using Microsoft.Extensions.DependencyInjection;
using StampliMCP.McpServer.Acumatica;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;
using StampliMCP.McpServer.Acumatica.Module.Services.Validation;

namespace StampliMCP.McpServer.Acumatica.Module;

public sealed class AcumaticaModule : IErpModule
{
    private static readonly IReadOnlyList<string> _aliases = Array.AsReadOnly(new[] { "acumatica", "acu" });
    private static readonly ErpCapability _capabilities =
        ErpCapability.Knowledge | ErpCapability.Flows | ErpCapability.Validation;

    public string Key => "acumatica";

    public IReadOnlyList<string> Aliases => _aliases;

    public ErpCapability Capabilities => _capabilities;

    public ErpDescriptor Descriptor { get; } = new(
        Key: "acumatica",
        Aliases: _aliases,
        Capabilities: _capabilities,
        Version: BuildInfo.VersionTag,
        Description: "Acumatica ERP knowledge base and tooling");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AcumaticaKnowledgeService>();
        services.AddSingleton<AcumaticaFlowService>();

        services.AddSingleton<FuzzyMatchingConfig>(_ => new FuzzyMatchingConfig
        {
            DefaultThreshold = 0.60,
            TypoToleranceThreshold = 0.70,
            OperationMatchThreshold = 0.60,
            ErrorMatchThreshold = 0.65,
            FlowMatchThreshold = 0.60,
            KeywordMatchThreshold = 0.60
        });
        services.AddSingleton<FuzzyMatchingService>();

        services.AddSingleton<SearchService>();
        services.AddSingleton<IntelligenceService>();
        services.AddSingleton<MetricsService>();
        services.AddSingleton<SmartFlowMatcher>();
        services.AddSingleton<JsonFileLogger>();
        services.AddSingleton<McpResponseLogger>();
        services.AddSingleton<IErpValidationService, AcumaticaValidationService>();
        services.AddSingleton<IErpDiagnosticService, AcumaticaDiagnosticService>();
        services.AddSingleton<IErpRecommendationService, AcumaticaRecommendationService>();
    }

    public IErpFacade CreateFacade(IServiceProvider services)
    {
        var scope = services.CreateScope();
        return new AcumaticaFacade(scope, Descriptor);
    }
}

file sealed class AcumaticaFacade(IServiceScope scope, ErpDescriptor descriptor) : ErpFacadeBase(scope, descriptor)
{
    public override KnowledgeServiceBase Knowledge => Services.GetRequiredService<AcumaticaKnowledgeService>();

    public override FlowServiceBase? Flow => Services.GetRequiredService<AcumaticaFlowService>();
}
