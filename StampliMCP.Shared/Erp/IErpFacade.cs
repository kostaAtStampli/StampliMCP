using Microsoft.Extensions.DependencyInjection;
using StampliMCP.Shared.Services;

namespace StampliMCP.Shared.Erp;

public interface IErpFacade : IDisposable
{
    ErpDescriptor Descriptor { get; }
    KnowledgeServiceBase Knowledge { get; }
    FlowServiceBase? Flow { get; }

    T GetRequiredService<T>() where T : class;
    T? GetService<T>() where T : class;
}

public abstract class ErpFacadeBase : IErpFacade
{
    private readonly IServiceScope _scope;

    protected ErpFacadeBase(IServiceScope scope, ErpDescriptor descriptor)
    {
        _scope = scope;
        Descriptor = descriptor;
    }

    protected IServiceProvider Services => _scope.ServiceProvider;

    public ErpDescriptor Descriptor { get; }

    public abstract KnowledgeServiceBase Knowledge { get; }

    public abstract FlowServiceBase? Flow { get; }

    public virtual T GetRequiredService<T>() where T : class
        => Services.GetRequiredService<T>();

    public virtual T? GetService<T>() where T : class
        => Services.GetService<T>();

    public void Dispose()
    {
        _scope.Dispose();
        GC.SuppressFinalize(this);
    }
}
