using Microsoft.Extensions.DependencyInjection;
using StampliMCP.McpServer.Intacct.Module.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Intacct.Module;

public sealed class IntacctModule : IErpModule
{
    private static readonly IReadOnlyList<string> _aliases = Array.AsReadOnly(new[] { "intacct", "ia" });
    private static readonly ErpCapability _capabilities = ErpCapability.Knowledge | ErpCapability.Flows;

    public string Key => "intacct";

    public IReadOnlyList<string> Aliases => _aliases;

    public ErpCapability Capabilities => _capabilities;

    public ErpDescriptor Descriptor { get; } = new(
        Key: "intacct",
        Aliases: _aliases,
        Capabilities: _capabilities,
        Version: "0.1-stub",
        Description: "Stub Intacct module for unified MCP validation");

    public void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IntacctKnowledgeService>();
        services.AddSingleton<IntacctFlowService>();
        services.AddSingleton<FuzzyMatchingConfig>(_ => new FuzzyMatchingConfig());
        services.AddSingleton<FuzzyMatchingService>();
    }

    public IErpFacade CreateFacade(IServiceProvider services)
    {
        var scope = services.CreateScope();
        return new IntacctFacade(scope, Descriptor);
    }
}

file sealed class IntacctFacade(IServiceScope scope, ErpDescriptor descriptor) : ErpFacadeBase(scope, descriptor)
{
    public override KnowledgeServiceBase Knowledge => Services.GetRequiredService<IntacctKnowledgeService>();

    public override FlowServiceBase? Flow => Services.GetRequiredService<IntacctFlowService>();
}
