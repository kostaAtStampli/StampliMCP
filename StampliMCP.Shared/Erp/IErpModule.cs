using Microsoft.Extensions.DependencyInjection;

namespace StampliMCP.Shared.Erp;

public interface IErpModule
{
    string Key { get; }
    IReadOnlyList<string> Aliases { get; }
    ErpCapability Capabilities { get; }
    ErpDescriptor Descriptor { get; }

    void RegisterServices(IServiceCollection services);
    IErpFacade CreateFacade(IServiceProvider services);
}
