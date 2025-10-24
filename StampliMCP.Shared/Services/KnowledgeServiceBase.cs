using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;

namespace StampliMCP.Shared.Services;

/// <summary>
/// Base class for ERP-specific knowledge services
/// Handles loading operations, categories, and other knowledge from embedded resources
/// </summary>
public abstract class KnowledgeServiceBase
{
    protected readonly ILogger Logger;
    protected readonly IMemoryCache Cache;
    protected readonly Assembly Assembly;
    private readonly ConcurrentDictionary<string, List<Operation>> _operationsByCategory = new();

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        Size = 1
    };

    protected KnowledgeServiceBase(ILogger logger, IMemoryCache cache, Assembly assembly)
    {
        Logger = logger;
        Cache = cache;
        Assembly = assembly;
    }

    /// <summary>
    /// Assembly resource prefix (e.g., "StampliMCP.McpServer.Acumatica.Knowledge")
    /// </summary>
    protected abstract string ResourcePrefix { get; }

    /// <summary>
    /// Map category names to knowledge file paths
    /// E.g., "vendors" -> "operations/vendors.json"
    /// </summary>
    protected abstract Dictionary<string, string> CategoryFileMapping { get; }

    protected async Task<string> ReadEmbeddedResourceAsync(string resourcePath, CancellationToken ct = default)
    {
        // Convert file path to embedded resource name
        // Knowledge/categories.json -> {ResourcePrefix}.categories.json
        var resourceName = $"{ResourcePrefix}.{resourcePath.Replace('/', '.').Replace('\\', '.')}";

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.LogWarning("Embedded resource not found: {ResourceName}", resourceName);
            throw new FileNotFoundException($"Resource {resourceName} not found in assembly");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_categories",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("categories.json", ct);
                    var data = JsonSerializer.Deserialize<CategoriesFile>(json, JsonOptions);
                    var categories = data?.Categories ?? [];
                    Logger.LogInformation("Loaded {Count} categories from embedded resources", categories.Count);
                    return categories;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading categories from embedded resources");
                    return new List<Category>();
                }
            }) ?? [];
    }

    public async Task<List<Operation>> GetOperationsByCategoryAsync(string category, CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_operations_{category}",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);

                    // Use ERP-specific category mapping
                    var knowledgeFile = CategoryFileMapping.GetValueOrDefault(category, $"operations/{category}.json");

                    var json = await ReadEmbeddedResourceAsync(knowledgeFile, ct);

                    // Parse the JSON structure which has operations as a nested object
                    var document = JsonDocument.Parse(json);
                    var ops = new List<Operation>();

                    if (document.RootElement.TryGetProperty("operations", out var operationsElement))
                    {
                        // Handle both array and object formats
                        if (operationsElement.ValueKind == JsonValueKind.Array)
                        {
                            // Array format: { "operations": [...] }
                            ops = JsonSerializer.Deserialize<List<Operation>>(operationsElement.GetRawText(), JsonOptions) ?? [];
                        }
                        else if (operationsElement.ValueKind == JsonValueKind.Object)
                        {
                            // Object format: { "operations": { "opName": {...}, ... } }
                            foreach (var opProperty in operationsElement.EnumerateObject())
                            {
                                try
                                {
                                    var element = opProperty.Value;
                                    string raw = element.GetRawText();

                                    // Normalize: inject "method" if missing but "operationName" exists
                                    bool hasMethod = element.TryGetProperty("method", out _);
                                    bool hasOperationName = element.TryGetProperty("operationName", out var opNameEl);

                                    if (!hasMethod && hasOperationName)
                                    {
                                        var node = System.Text.Json.Nodes.JsonNode.Parse(raw) as System.Text.Json.Nodes.JsonObject;
                                        if (node != null && !node.ContainsKey("method"))
                                        {
                                            node["method"] = opNameEl.GetString();
                                            raw = node.ToJsonString();
                                        }
                                    }

                                    var op = JsonSerializer.Deserialize<Operation>(raw, JsonOptions);
                                    if (op != null)
                                    {
                                        ops.Add(op);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogWarning(ex, "Failed to parse operation entry for category {Category}", category);
                                }
                            }
                        }
                    }

                    // Also store in concurrent dictionary for fast lookup
                    _operationsByCategory.TryAdd(category, ops);

                    Logger.LogInformation("Loaded {Count} operations for {Category} from {File}", ops.Count, category, knowledgeFile);
                    return ops;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading operations for category {Category} from embedded resources", category);
                    return new List<Operation>();
                }
            }) ?? [];
    }

    public async Task<Operation?> FindOperationAsync(string methodName, CancellationToken ct = default)
    {
        var categories = await GetCategoriesAsync(ct);

        foreach (var category in categories)
        {
            var ops = await GetOperationsByCategoryAsync(category.Name, ct);
            var op = ops.FirstOrDefault(o => o.Method.Equals(methodName, StringComparison.OrdinalIgnoreCase));
            if (op is not null) return op;
        }

        return null;
    }

    public async Task<List<Operation>> GetAllOperationsAsync(CancellationToken ct = default)
    {
        var categories = await GetCategoriesAsync(ct);
        var allOperations = new List<Operation>();

        foreach (var category in categories)
        {
            var ops = await GetOperationsByCategoryAsync(category.Name, ct);
            allOperations.AddRange(ops);
        }

        return allOperations;
    }

    public async Task<List<EnumMapping>> GetEnumsAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_enums",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("enums.json", ct);
                    var data = JsonSerializer.Deserialize<EnumsFile>(json, JsonOptions);
                    var enums = data?.Enums ?? [];
                    Logger.LogInformation("Loaded {Count} enum mappings from embedded resources", enums.Count);
                    return enums;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading enums from embedded resources");
                    return new List<EnumMapping>();
                }
            }) ?? [];
    }
}

file sealed record EnumsFile(List<EnumMapping> Enums);
