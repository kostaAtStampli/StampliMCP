using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Services;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Acumatica-specific knowledge service
/// Inherits generic knowledge loading from base class
/// </summary>
public sealed class AcumaticaKnowledgeService : KnowledgeServiceBase
{
    public AcumaticaKnowledgeService(ILogger<AcumaticaKnowledgeService> logger, IMemoryCache cache)
        : base(logger, cache, typeof(AcumaticaKnowledgeService).Assembly)
    {
    }

    protected override string ResourcePrefix => "StampliMCP.McpServer.Acumatica.Module.Knowledge";

    protected override Dictionary<string, string> CategoryFileMapping => new()
    {
        ["payments"] = "operations/payments.json",
        ["purchaseOrders"] = "operations/purchaseOrders.json",
        ["accounts"] = "operations/accounts.json",
        ["fields"] = "operations/fields.json",
        ["customFields"] = "custom-field-operations.json",
        ["admin"] = "operations/admin.json",
        ["vendors"] = "operations/vendors.json",
        ["items"] = "operations/items.json",
        ["retrieval"] = "operations/retrieval.json",
        ["utility"] = "operations/utility.json",
        ["other"] = "operations/other.json"
    };

    // Acumatica-specific methods (not generic to all ERPs)

    public async Task<object> GetTestConfigAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_test-config",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("test-config.json", ct);
                    var config = System.Text.Json.JsonSerializer.Deserialize<object>(json) ?? new { };
                    Logger.LogInformation("Loaded test configuration from embedded resources");
                    return config;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading test configuration from embedded resources");
                    return new { error = "Failed to load test configuration" };
                }
            }) ?? new { };
    }

    public async Task<object> GetKotlinGoldenReferenceAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_kotlin-golden-reference",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("kotlin-golden-reference.json", ct);
                    var reference = System.Text.Json.JsonSerializer.Deserialize<object>(json, JsonOptions);
                    Logger.LogInformation("Loaded Kotlin golden reference (exportVendor example) from embedded resources");
                    return reference ?? new { };
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading Kotlin golden reference from embedded resources");
                    return new { error = "Failed to load Kotlin golden reference" };
                }
            }) ?? new { };
    }

    public async Task<string> GetKotlinGoldenPatternsAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_kotlin-golden-patterns",
            async entry =>
            {
                entry.SetOptions(CacheOptions);
                var content = await ReadEmbeddedResourceAsync("kotlin.GOLDEN_PATTERNS.md", ct);
                Logger.LogInformation("Loaded Kotlin golden patterns from embedded resources");
                return content;
            }) ?? string.Empty;
    }

    public async Task<string> GetKotlinTddWorkflowAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_kotlin-tdd-workflow",
            async entry =>
            {
                entry.SetOptions(CacheOptions);
                var content = await ReadEmbeddedResourceAsync("kotlin.TDD_WORKFLOW.md", ct);
                Logger.LogInformation("Loaded Kotlin TDD workflow from embedded resources");
                return content;
            }) ?? string.Empty;
    }

    public async Task<object> GetKotlinErrorPatternsAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_kotlin-error-patterns",
            async entry =>
            {
                entry.SetOptions(CacheOptions);
                var json = await ReadEmbeddedResourceAsync("kotlin.error-patterns-kotlin.json", ct);
                var patterns = JsonSerializer.Deserialize<object>(json) ?? new { };
                Logger.LogInformation("Loaded Kotlin error patterns from embedded resources");
                return patterns;
            }) ?? new { };
    }

    public async Task<JsonDocument?> GetModernInfrastructureAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_modern-infrastructure",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("modern-infrastructure.json", ct);
                    var document = JsonDocument.Parse(json);
                    Logger.LogInformation("Loaded modern infrastructure (LiveErpTestBase, DSLs, ENV1) from embedded resources");
                    return document;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading modern infrastructure from embedded resources");
                    return null;
                }
            });
    }

    public async Task<ErrorCatalog> GetErrorCatalogAsync(CancellationToken ct = default)
    {
        return await Cache.GetOrCreateAsync(
            $"{ResourcePrefix}_error-catalog",
            async entry =>
            {
                try
                {
                    entry.SetOptions(CacheOptions);
                    var json = await ReadEmbeddedResourceAsync("error-catalog.json", ct);
                    var catalog = JsonSerializer.Deserialize<ErrorCatalog>(json, JsonOptions);
                    Logger.LogInformation("Loaded error catalog from embedded resources");
                    return catalog ?? new ErrorCatalog();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error loading error catalog from embedded resources");
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
