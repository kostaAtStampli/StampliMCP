using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace StampliMCP.Shared.Services;

/// <summary>
/// Base class for flow services across all ERP MCP servers
/// Handles generic flow loading from embedded resources
/// </summary>
public abstract class FlowServiceBase
{
    protected readonly ILogger Logger;
    protected readonly IMemoryCache Cache;
    protected readonly FuzzyMatchingService FuzzyMatcher;
    protected readonly Assembly Assembly;

    private readonly ConcurrentDictionary<string, string> _operationToFlow = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<string>> _flowToOperations = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _indexSemaphore = new(1, 1);
    private volatile bool _indexesBuilt;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        Size = 1
    };

    /// <summary>
    /// Resource prefix for embedded flow files (e.g., "StampliMCP.McpServer.Acumatica.Knowledge.flows")
    /// </summary>
    protected abstract string FlowResourcePrefix { get; }

    protected FlowServiceBase(
        ILogger logger,
        IMemoryCache cache,
        FuzzyMatchingService fuzzyMatcher,
        Assembly assembly)
    {
        Logger = logger;
        Cache = cache;
        FuzzyMatcher = fuzzyMatcher;
        Assembly = assembly;
    }

    protected async Task<string> ReadEmbeddedResourceAsync(string resourcePath, CancellationToken ct = default)
    {
        var resourceName = $"{FlowResourcePrefix}.{resourcePath}";
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.LogWarning("Flow resource not found: {ResourceName}", resourceName);
            throw new FileNotFoundException($"Flow resource {resourceName} not found");
        }
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public virtual async Task<JsonDocument?> GetFlowAsync(string flowName, CancellationToken ct = default)
    {
        // Normalize requested name to canonical embedded resource name (case-insensitive)
        var all = await GetAllFlowNamesAsync(ct);
        var normalized = all.FirstOrDefault(n => n.Equals(flowName, StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
        {
            // Try common transformations (UPPER_SNAKE to lower_snake)
            var candidate = flowName.Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
            normalized = all.FirstOrDefault(n => n.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }

        var cacheKey = $"{FlowResourcePrefix}_flow_{normalized ?? flowName}";

        return await Cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var resourceFile = $"{(normalized ?? flowName)}.json";
                    var json = await ReadEmbeddedResourceAsync(resourceFile, ct);
                    var doc = JsonDocument.Parse(json);
                    Logger.LogDebug("Loaded flow {FlowName}", normalized ?? flowName);
                    return doc;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading flow {FlowName}", flowName);
                    return null;
                }
            });
    }

    public virtual async Task<List<string>> GetAllFlowNamesAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{FlowResourcePrefix}_all_flow_names",
            entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var flowNames = Assembly.GetManifestResourceNames()
                        .Where(r => r.StartsWith($"{FlowResourcePrefix}."))
                        .Select(r => r.Replace($"{FlowResourcePrefix}.", "")
                                      .Replace(".json", ""))
                        .ToList();
                    Logger.LogDebug("Found {Count} flows", flowNames.Count);
                    return Task.FromResult(flowNames);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error listing flows");
                    return Task.FromResult(new List<string>());
                }
            }) ?? new List<string>();
    }

    public virtual async Task<string?> GetFlowForOperationAsync(string operationName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return null;
        }

        await EnsureIndexesAsync(ct);
        if (_operationToFlow.TryGetValue(operationName, out var cachedFlow))
        {
            return cachedFlow;
        }

        var flowNames = await GetAllFlowNamesAsync(ct);
        foreach (var flowName in flowNames)
        {
            var flow = await GetFlowAsync(flowName, ct);
            if (flow == null) continue;

            var usedByOps = flow.RootElement.GetProperty("usedByOperations");
            foreach (var op in usedByOps.EnumerateArray())
            {
                var opName = op.GetString();
                if (string.IsNullOrWhiteSpace(opName))
                {
                    continue;
                }

                RegisterOperationFlow(flowName, opName);

                if (opName.Equals(operationName, StringComparison.OrdinalIgnoreCase))
                {
                    return flowName;
                }
            }
        }
        return null;
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesBuilt)
        {
            return;
        }

        await _indexSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_indexesBuilt)
            {
                return;
            }

            var operationMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var flowMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var flowNames = await GetAllFlowNamesAsync(ct).ConfigureAwait(false);
            foreach (var flowName in flowNames)
            {
                var flow = await GetFlowAsync(flowName, ct).ConfigureAwait(false);
                if (flow is null)
                {
                    continue;
                }

                if (flow.RootElement.TryGetProperty("usedByOperations", out var usedBy))
                {
                    foreach (var opElement in usedBy.EnumerateArray())
                    {
                        var opName = opElement.GetString();
                        if (string.IsNullOrWhiteSpace(opName))
                        {
                            continue;
                        }

                        if (!operationMap.ContainsKey(opName))
                        {
                            operationMap[opName] = flowName;
                        }

                        if (!flowMap.TryGetValue(flowName, out var list))
                        {
                            list = new List<string>();
                            flowMap[flowName] = list;
                        }

                        if (!list.Any(o => o.Equals(opName, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(opName);
                        }
                    }
                }
            }

            _operationToFlow.Clear();
            _flowToOperations.Clear();

            foreach (var (key, value) in operationMap)
            {
                _operationToFlow[key] = value;
            }

            foreach (var (flowName, operations) in flowMap)
            {
                _flowToOperations[flowName] = operations;
            }

            _indexesBuilt = true;
        }
        finally
        {
            _indexSemaphore.Release();
        }
    }

    protected IReadOnlyList<string> GetCachedOperationsForFlow(string flowName)
    {
        if (_flowToOperations.TryGetValue(flowName, out var ops))
        {
            return ops;
        }

        return Array.Empty<string>();
    }

    private void RegisterOperationFlow(string flowName, string operationName)
    {
        _operationToFlow.TryAdd(operationName, flowName);

        _flowToOperations.AddOrUpdate(
            flowName,
            _ => new List<string> { operationName },
            (_, list) =>
            {
                if (!list.Any(o => o.Equals(operationName, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(operationName);
                }
                return list;
            });
    }

    protected bool ContainsOrFuzzy(string text, string keyword, double threshold)
    {
        // Fast path: exact match
        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // Fuzzy path: Check if any word in text fuzzy-matches keyword
        var words = text.Split([' ', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var matches = FuzzyMatcher.FindAllMatches(keyword, words, threshold);
        return matches.Any();
    }
}
