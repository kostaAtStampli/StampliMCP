using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class FlowService(ILogger<FlowService> logger, IMemoryCache cache)
{
    private readonly Assembly _assembly = typeof(FlowService).Assembly;
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
        var resourceName = $"StampliMCP.McpServer.Acumatica.Knowledge.flows.{resourcePath}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            logger.LogWarning("Flow resource not found: {ResourceName}", resourceName);
            throw new FileNotFoundException($"Flow resource {resourceName} not found");
        }
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<JsonDocument?> GetFlowAsync(string flowName, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"flow_{flowName}",
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var json = await ReadEmbeddedResourceAsync($"{flowName}.json", ct);
                    var doc = JsonDocument.Parse(json);
                    logger.LogInformation("Loaded flow {FlowName}", flowName);
                    return doc;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error loading flow {FlowName}", flowName);
                    return null;
                }
            });
    }

    public async Task<List<string>> GetAllFlowNamesAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "all_flow_names",
            entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var flowNames = _assembly.GetManifestResourceNames()
                        .Where(r => r.StartsWith("StampliMCP.McpServer.Acumatica.Knowledge.flows."))
                        .Select(r => r.Replace("StampliMCP.McpServer.Acumatica.Knowledge.flows.", "")
                                      .Replace(".json", ""))
                        .ToList();
                    logger.LogInformation("Found {Count} flows", flowNames.Count);
                    return Task.FromResult(flowNames);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error listing flows");
                    return Task.FromResult(new List<string>());
                }
            }) ?? new List<string>();
    }

    public async Task<(string FlowName, string Confidence, string Reasoning)> MatchFeatureToFlowAsync(
        string description,
        CancellationToken ct = default)
    {
        var lower = description.ToLower();

        // Flow detection logic (keyword-based)
        // Export flows (check first - more specific)
        if (lower.Contains("vendor") && (lower.Contains("export") || lower.Contains("create")))
            return ("vendor_export_flow", "HIGH", "User wants to export/create vendor");

        if ((lower.Contains("bill") || lower.Contains("invoice")) &&
            (lower.Contains("export") || lower.Contains("create")))
            return ("export_invoice_flow", "HIGH", "User wants to export bill/invoice");

        if (lower.Contains("payment") && (lower.Contains("export") || lower.Contains("create")))
            return ("payment_flow", "HIGH", "User wants to export payment");

        if (lower.Contains("purchase order") && (lower.Contains("export") || lower.Contains("create")))
            return ("export_po_flow", "HIGH", "User wants to export purchase order");

        // PO matching flows
        if (lower.Contains("po matching") && (lower.Contains("all") || lower.Contains("closed")))
            return ("po_matching_full_import_flow", "HIGH", "User wants full PO matching with closed data");

        if (lower.Contains("po matching") || (lower.Contains("purchase order") && lower.Contains("match")))
            return ("po_matching_flow", "MEDIUM", "User wants single PO matching");

        // M2M flow
        if (lower.Contains("m2m") || lower.Contains("many to many") || lower.Contains("relationship") ||
            (lower.Contains("branch") && lower.Contains("project")) ||
            (lower.Contains("project") && lower.Contains("task")) ||
            (lower.Contains("task") && lower.Contains("cost code")))
            return ("m2m_import_flow", "HIGH", "User wants M2M relationship import");

        // API actions
        if (lower.Contains("void") || lower.Contains("release") || lower.Contains("action"))
            return ("api_action_flow", "MEDIUM", "User wants to perform API action (void/release)");

        // Default: Standard import (most common)
        if (lower.Contains("import") || lower.Contains("get") || lower.Contains("retrieve") ||
            lower.Contains("fetch") || lower.Contains("custom field") || lower.Contains("vendor") ||
            lower.Contains("account") || lower.Contains("item") || lower.Contains("tax"))
            return ("standard_import_flow", "HIGH", "User wants to import data using standard pagination pattern");

        // Fallback
        return ("standard_import_flow", "LOW", "No specific flow detected, defaulting to standard import");
    }

    public async Task<string?> GetFlowForOperationAsync(string operationName, CancellationToken ct = default)
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
}
