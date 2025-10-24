using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

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
                    Logger.LogInformation("Loaded flow {FlowName}", normalized ?? flowName);
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
                    Logger.LogInformation("Found {Count} flows", flowNames.Count);
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
        var flowNames = await GetAllFlowNamesAsync(ct);
        foreach (var flowName in flowNames)
        {
            var flow = await GetFlowAsync(flowName, ct);
            if (flow == null) continue;

            var usedByOps = flow.RootElement.GetProperty("usedByOperations");
            foreach (var op in usedByOps.EnumerateArray())
            {
                if (op.GetString()?.Equals(operationName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return flowName;
                }
            }
        }
        return null;
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
