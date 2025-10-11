using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class KnowledgeService(ILogger<KnowledgeService> logger, IMemoryCache cache)
{
    private readonly string _knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");
    private readonly ConcurrentDictionary<string, List<Operation>> _operationsByCategory = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly MemoryCacheEntryOptions _cacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        Size = 1
    };

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "categories",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "categories.json"), ct);
                var data = JsonSerializer.Deserialize<CategoriesFile>(json, _jsonOptions);
                var categories = data?.Categories ?? [];
                logger.LogInformation("Loaded {Count} categories", categories.Count);
                return categories;
            }) ?? [];
    }

    public async Task<List<Operation>> GetOperationsByCategoryAsync(string category, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"operations_{category}",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);

                var filePath = Path.Combine(_knowledgePath, "operations", $"{category}.json");
                if (!File.Exists(filePath)) return new List<Operation>();

                var json = await File.ReadAllTextAsync(filePath, ct);
                var data = JsonSerializer.Deserialize<OperationsFile>(json, _jsonOptions);
                var ops = data?.Operations ?? [];

                // Also store in concurrent dictionary for fast lookup
                _operationsByCategory.TryAdd(category, ops);

                logger.LogInformation("Loaded {Count} operations for {Category}", ops.Count, category);
                return ops;
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

    public async Task<List<EnumMapping>> GetEnumsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "enums",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "enums.json"), ct);
                var data = JsonSerializer.Deserialize<EnumsFile>(json, _jsonOptions);
                var enums = data?.Enums ?? [];
                logger.LogInformation("Loaded {Count} enum mappings", enums.Count);
                return enums;
            }) ?? [];
    }

    public async Task<object> GetTestConfigAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "test-config",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "test-config.json"), ct);
                var config = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded test configuration");
                return config;
            }) ?? new { };
    }
}

file sealed record CategoriesFile(List<Category> Categories);
file sealed record OperationsFile(string Category, List<Operation> Operations);
file sealed record EnumsFile(List<EnumMapping> Enums);

