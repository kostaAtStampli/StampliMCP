using System.Linq;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Erp;

namespace StampliMCP.McpServer.Unified.Services;

public sealed class ErpRegistry
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ErpRegistry> _logger;
    private readonly IReadOnlyDictionary<string, IErpModule> _modules;
    private readonly IReadOnlyDictionary<string, string> _aliasMap;
    private readonly IReadOnlyDictionary<string, ErpDescriptor> _descriptors;

    public ErpRegistry(IServiceProvider services, IEnumerable<IErpModule> modules, ILogger<ErpRegistry> logger)
    {
        _services = services;
        _logger = logger;

        var moduleDict = new Dictionary<string, IErpModule>(StringComparer.OrdinalIgnoreCase);
        var aliasDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var descriptorDict = new Dictionary<string, ErpDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            var key = module.Key.ToLowerInvariant();
            if (moduleDict.ContainsKey(key))
            {
                throw new InvalidOperationException($"Duplicate ERP module key detected: {module.Key}");
            }

            moduleDict[key] = module;
            var descriptor = module.Descriptor ?? throw new InvalidOperationException($"ERP module '{module.Key}' returned null descriptor");
            descriptorDict[key] = descriptor;

            foreach (var alias in module.Aliases)
            {
                var normalized = alias.ToLowerInvariant();
                if (aliasDict.ContainsKey(normalized))
                {
                    throw new InvalidOperationException($"ERP alias '{alias}' is already mapped to '{aliasDict[normalized]}'");
                }
                aliasDict[normalized] = key;
            }
        }

        _modules = moduleDict;
        _aliasMap = aliasDict;
        _descriptors = descriptorDict;

        _logger.LogInformation("ERP registry initialized with {Count} modules", _modules.Count);
    }

    public IReadOnlyCollection<ErpDescriptor> ListErps() => _descriptors.Values.ToArray();

    public bool TryGetDescriptor(string erp, out ErpDescriptor? descriptor)
    {
        if (string.IsNullOrWhiteSpace(erp))
        {
            descriptor = default!;
            return false;
        }

        var key = Normalize(erp);
        return _descriptors.TryGetValue(key, out descriptor);
    }

    public IErpFacade GetFacade(string erp)
    {
        if (string.IsNullOrWhiteSpace(erp))
        {
            throw new ArgumentException("ERP identifier is required", nameof(erp));
        }

        var key = Normalize(erp);

        if (!_modules.TryGetValue(key, out var module))
        {
            throw new KeyNotFoundException($"ERP '{erp}' not registered. Known ERPs: {string.Join(", ", _modules.Keys)}");
        }

        _logger.LogDebug("Creating ERP facade for {Erp}", key);
        return module.CreateFacade(_services);
    }

    public string Normalize(string erp)
    {
        var normalized = erp.Trim().ToLowerInvariant();
        if (_modules.ContainsKey(normalized))
        {
            return normalized;
        }

        if (_aliasMap.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        var suggestions = _modules.Keys
            .Where(k => k.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (suggestions.Length == 0)
        {
            throw new KeyNotFoundException($"ERP '{erp}' not registered. Known ERPs: {string.Join(", ", _modules.Keys)}");
        }

        _logger.LogInformation("ERP '{Erp}' normalized to '{Suggestion}'", erp, suggestions[0]);
        return suggestions[0];
    }
}
