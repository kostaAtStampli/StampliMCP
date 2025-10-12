using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class KnowledgeService(ILogger<KnowledgeService> logger, IMemoryCache cache)
{
    private readonly Assembly _assembly = typeof(KnowledgeService).Assembly;
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

    private async Task<string> ReadEmbeddedResourceAsync(string resourcePath, CancellationToken ct = default)
    {
        // Convert file path to embedded resource name
        // Knowledge/categories.json -> StampliMCP.McpServer.Acumatica.Knowledge.categories.json
        var resourceName = $"StampliMCP.McpServer.Acumatica.Knowledge.{resourcePath.Replace('/', '.').Replace('\\', '.')}";

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            logger.LogWarning("Embedded resource not found: {ResourceName}", resourceName);
            throw new FileNotFoundException($"Resource {resourceName} not found in assembly");
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "categories",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync("categories.json", ct);
                    var data = JsonSerializer.Deserialize<CategoriesFile>(json, _jsonOptions);
                    var categories = data?.Categories ?? [];
                    logger.LogInformation("Loaded {Count} categories from embedded resources", categories.Count);
                    return categories;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading categories from embedded resources");
                    return new List<Category>();
                }
            }) ?? [];
    }

    public async Task<List<Operation>> GetOperationsByCategoryAsync(string category, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"operations_{category}",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync($"operations.{category}.json", ct);
                    var data = JsonSerializer.Deserialize<OperationsFile>(json, _jsonOptions);
                    var ops = data?.Operations ?? [];

                    // Also store in concurrent dictionary for fast lookup
                    _operationsByCategory.TryAdd(category, ops);

                    logger.LogInformation("Loaded {Count} operations for {Category} from embedded resources", ops.Count, category);
                    return ops;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading operations for category {Category} from embedded resources", category);
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
        return await cache.GetOrCreateAsync(
            "enums",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync("enums.json", ct);
                    var data = JsonSerializer.Deserialize<EnumsFile>(json, _jsonOptions);
                    var enums = data?.Enums ?? [];
                    logger.LogInformation("Loaded {Count} enum mappings from embedded resources", enums.Count);
                    return enums;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading enums from embedded resources");
                    return new List<EnumMapping>();
                }
            }) ?? [];
    }

    public async Task<object> GetTestConfigAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "test-config",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync("test-config.json", ct);
                    var config = JsonSerializer.Deserialize<object>(json) ?? new { };
                    logger.LogInformation("Loaded test configuration from embedded resources");
                    return config;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading test configuration from embedded resources");
                    return new { error = "Failed to load test configuration" };
                }
            }) ?? new { };
    }

    public async Task<object> GetKotlinErrorPatternsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "kotlin-error-patterns",
            async entry =>
            {
                entry.SetOptions(_cacheOptions);
                var json = await ReadEmbeddedResourceAsync("kotlin.error-patterns-kotlin.json", ct);
                var patterns = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin error patterns from embedded resources");
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
                var json = await ReadEmbeddedResourceAsync("kotlin.kotlin-integration.json", ct);
                var integration = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin integration strategy from embedded resources");
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
                var json = await ReadEmbeddedResourceAsync("kotlin.method-signatures.json", ct);
                var signatures = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin method signatures from embedded resources");
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
                var json = await ReadEmbeddedResourceAsync("kotlin.test-config-kotlin.json", ct);
                var config = JsonSerializer.Deserialize<object>(json) ?? new { };
                logger.LogInformation("Loaded Kotlin test configuration from embedded resources");
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
                var content = await ReadEmbeddedResourceAsync("kotlin.GOLDEN_PATTERNS.md", ct);
                logger.LogInformation("Loaded Kotlin golden patterns from embedded resources");
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
                var content = await ReadEmbeddedResourceAsync("kotlin.KOTLIN_ARCHITECTURE.md", ct);
                logger.LogInformation("Loaded Kotlin architecture guide from embedded resources");
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
                var content = await ReadEmbeddedResourceAsync("kotlin.TDD_WORKFLOW.md", ct);
                logger.LogInformation("Loaded Kotlin TDD workflow from embedded resources");
                return content;
            }) ?? string.Empty;
    }

    public async Task<ErrorCatalog> GetErrorCatalogAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "error-catalog",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync("error-catalog.json", ct);
                    var catalog = JsonSerializer.Deserialize<ErrorCatalog>(json, _jsonOptions);
                    logger.LogInformation("Loaded error catalog from embedded resources");
                    return catalog ?? new ErrorCatalog();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading error catalog from embedded resources");
                    return new ErrorCatalog();
                }
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

