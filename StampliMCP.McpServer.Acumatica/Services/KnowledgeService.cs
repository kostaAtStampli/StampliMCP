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

    public async Task<object> GetKotlinErrorPatternsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-error-patterns",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "error-patterns-kotlin.json"), ct);
                var patterns = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin error patterns");
                return patterns;
            }) ?? new { };
    }

    public async Task<object> GetKotlinIntegrationAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-integration",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "kotlin-integration.json"), ct);
                var integration = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin integration strategy");
                return integration;
            }) ?? new { };
    }

    public async Task<object> GetKotlinMethodSignaturesAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-method-signatures",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "method-signatures.json"), ct);
                var signatures = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin method signatures");
                return signatures;
            }) ?? new { };
    }

    public async Task<object> GetKotlinTestConfigAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-test-config",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "test-config-kotlin.json"), ct);
                var config = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin test configuration");
                return config;
            }) ?? new { };
    }

    public async Task<string> GetKotlinGoldenPatternsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-golden-patterns",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var content = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "GOLDEN_PATTERNS.md"), ct);
                logger.LogInformation("Loaded Kotlin golden patterns");
                return content;
            }) ?? string.Empty;
    }

    public async Task<string> GetKotlinArchitectureAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-architecture",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var content = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "KOTLIN_ARCHITECTURE.md"), ct);
                logger.LogInformation("Loaded Kotlin architecture guide");
                return content;
            }) ?? string.Empty;
    }

    public async Task<string> GetKotlinTddWorkflowAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-tdd-workflow",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var content = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "kotlin", "TDD_WORKFLOW.md"), ct);
                logger.LogInformation("Loaded Kotlin TDD workflow");
                return content;
            }) ?? string.Empty;
    }

    public async Task<ErrorCatalog> GetErrorCatalogAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "error-catalog",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await File.ReadAllTextAsync(
                    Path.Combine(_knowledgePath, "error-catalog.json"), ct);
                var catalog = JsonSerializer.Deserialize<ErrorCatalog>(json, _jsonOptions);
                logger.LogInformation("Loaded error catalog");
                return catalog ?? new ErrorCatalog();
            }) ?? new ErrorCatalog();
    }

    public async Task<List<ErrorDetail>> GetOperationErrorsAsync(string operationMethod, CancellationToken ct = default)
    {
        var catalog = await GetErrorCatalogAsync(ct);
        var errors = new List<ErrorDetail>();

        if (catalog.OperationErrors != null && catalog.OperationErrors.TryGetValue(operationMethod, out var opErrors))
        {
            if (opErrors.Validation != null)
                errors.AddRange(opErrors.Validation);
            if (opErrors.BusinessLogic != null)
                errors.AddRange(opErrors.BusinessLogic);
        }

        return errors;
    }
}

file sealed record CategoriesFile(List<Category> Categories);
file sealed record OperationsFile(string Category, List<Operation> Operations);
file sealed record EnumsFile(List<EnumMapping> Enums);

// Error catalog models
public sealed record ErrorCatalog
{
    public List<ErrorDetail>? AuthenticationErrors { get; init; }
    public Dictionary<string, OperationErrorSet>? OperationErrors { get; init; }
    public List<ApiError>? ApiErrors { get; init; }
}

public sealed record OperationErrorSet
{
    public List<ErrorDetail>? Validation { get; init; }
    public List<ErrorDetail>? BusinessLogic { get; init; }
}

public sealed record ErrorDetail
{
    public string? Field { get; init; }
    public string? Condition { get; init; }
    public string? Type { get; init; }
    public required string Message { get; init; }
    public CodeLocation? Location { get; init; }
    public string? TestAssertion { get; init; }
}

public sealed record ApiError
{
    public int Code { get; init; }
    public required string Message { get; init; }
    public string? Handling { get; init; }
    public CodeLocation? Location { get; init; }
}

public sealed record CodeLocation
{
    public required string File { get; init; }
    public string? Lines { get; init; }
}

