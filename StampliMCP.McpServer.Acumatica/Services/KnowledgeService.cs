using System.Text.Json;
using Microsoft.Extensions.Logging;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class KnowledgeService(ILogger<KnowledgeService> logger)
{
    private readonly string _knowledgePath = Path.Combine(AppContext.BaseDirectory, "Knowledge");
    private List<Category>? _categories;
    private readonly Dictionary<string, List<Operation>> _operationsByCategory = [];
    private static readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (_categories is not null) return _categories;
        
        var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "categories.json"), ct);
        var data = JsonSerializer.Deserialize<CategoriesFile>(json, _jsonOptions);
        _categories = data?.Categories ?? [];
        logger.LogInformation("Loaded {Count} categories", _categories.Count);
        return _categories;
    }

    public async Task<List<Operation>> GetOperationsByCategoryAsync(string category, CancellationToken ct = default)
    {
        if (_operationsByCategory.TryGetValue(category, out var cached))
            return cached;

        var filePath = Path.Combine(_knowledgePath, "operations", $"{category}.json");
        if (!File.Exists(filePath)) return [];

        var json = await File.ReadAllTextAsync(filePath, ct);
        var data = JsonSerializer.Deserialize<OperationsFile>(json, _jsonOptions);
        var ops = data?.Operations ?? [];
        _operationsByCategory[category] = ops;
        
        logger.LogInformation("Loaded {Count} operations for {Category}", ops.Count, category);
        return ops;
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
        var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "enums.json"), ct);
        var data = JsonSerializer.Deserialize<EnumsFile>(json, _jsonOptions);
        return data?.Enums ?? [];
    }

    public async Task<object> GetTestConfigAsync(CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(Path.Combine(_knowledgePath, "test-config.json"), ct);
        return JsonSerializer.Deserialize<object>(json) ?? new { };
    }
}

file sealed record CategoriesFile(List<Category> Categories);
file sealed record OperationsFile(string Category, List<Operation> Operations);
file sealed record EnumsFile(List<EnumMapping> Enums);

