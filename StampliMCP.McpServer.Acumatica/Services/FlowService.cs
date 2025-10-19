using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class FlowService
{
    private readonly ILogger<FlowService> _logger;
    private readonly IMemoryCache _cache;
    private readonly FuzzyMatchingService _fuzzyMatcher;
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

    public FlowService(ILogger<FlowService> logger, IMemoryCache cache, FuzzyMatchingService fuzzyMatcher)
    {
        _logger = logger;
        _cache = cache;
        _fuzzyMatcher = fuzzyMatcher;
    }

    private async Task<string> ReadEmbeddedResourceAsync(string resourcePath, CancellationToken ct = default)
    {
        var resourceName = $"StampliMCP.McpServer.Acumatica.Knowledge.flows.{resourcePath}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogWarning("Flow resource not found: {ResourceName}", resourceName);
            throw new FileNotFoundException($"Flow resource {resourceName} not found");
        }
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<JsonDocument?> GetFlowAsync(string flowName, CancellationToken ct = default)
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

        var cacheKey = $"flow_{normalized ?? flowName}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                try
                {
                    entry.SetOptions(_cacheOptions);
                    var resourceFile = $"{(normalized ?? flowName)}.json";
                    var json = await ReadEmbeddedResourceAsync(resourceFile, ct);
                    var doc = JsonDocument.Parse(json);
                    _logger.LogInformation("Loaded flow {FlowName}", normalized ?? flowName);
                    return doc;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading flow {FlowName}", flowName);
                    return null;
                }
            });
    }

    public async Task<List<string>> GetAllFlowNamesAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync(
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
                    _logger.LogInformation("Found {Count} flows", flowNames.Count);
                    return Task.FromResult(flowNames);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error listing flows");
                    return Task.FromResult(new List<string>());
                }
            }) ?? new List<string>();
    }

    public (string FlowName, string Confidence, string Reasoning) MatchFeatureToFlowAsync(
        string description,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var lower = description.ToLower();

        // Flow detection logic with fuzzy matching for typos (threshold 0.60)
        // Export flows (check first - more specific)
        if (ContainsOrFuzzy(lower, "vendor", 0.60) && (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: vendor_export_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("vendor_export_flow", "HIGH", "User wants to export/create vendor");
        }

        if ((ContainsOrFuzzy(lower, "bill", 0.60) || ContainsOrFuzzy(lower, "invoice", 0.60)) &&
            (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: export_invoice_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("export_invoice_flow", "HIGH", "User wants to export bill/invoice");
        }

        if (ContainsOrFuzzy(lower, "payment", 0.60) && (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: payment_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("payment_flow", "HIGH", "User wants to export payment");
        }

        if (ContainsOrFuzzy(lower, "purchase order", 0.60) && (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: export_po_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("export_po_flow", "HIGH", "User wants to export purchase order");
        }

        // PO matching flows
        if (ContainsOrFuzzy(lower, "po matching", 0.60) && (lower.Contains("all") || lower.Contains("closed")))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: po_matching_full_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("po_matching_full_import_flow", "HIGH", "User wants full PO matching with closed data");
        }

        if (ContainsOrFuzzy(lower, "po matching", 0.60) || (ContainsOrFuzzy(lower, "purchase order", 0.60) && lower.Contains("match")))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: po_matching_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("po_matching_flow", "MEDIUM", "User wants single PO matching");
        }

        // M2M flow
        if (lower.Contains("m2m") || lower.Contains("many to many") || lower.Contains("relationship") ||
            (lower.Contains("branch") && lower.Contains("project")) ||
            (lower.Contains("project") && lower.Contains("task")) ||
            (lower.Contains("task") && lower.Contains("cost code")))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: m2m_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("m2m_import_flow", "HIGH", "User wants M2M relationship import");
        }

        // API actions
        if (lower.Contains("void") || lower.Contains("release") || lower.Contains("action"))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: api_action_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("api_action_flow", "MEDIUM", "User wants to perform API action (void/release)");
        }

        // Default: Standard import (most common)
        if (ContainsOrFuzzy(lower, "import", 0.60) || lower.Contains("get") || lower.Contains("retrieve") ||
            lower.Contains("fetch") || lower.Contains("custom field") || ContainsOrFuzzy(lower, "vendor", 0.60) ||
            lower.Contains("account") || ContainsOrFuzzy(lower, "item", 0.60) || lower.Contains("tax"))
        {
            sw.Stop();
            _logger.LogInformation("FlowMatch: standard_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("standard_import_flow", "HIGH", "User wants to import data using standard pagination pattern");
        }

        // Fallback
        sw.Stop();
        _logger.LogInformation("FlowMatch: standard_import_flow (fallback), time={Ms}ms", sw.ElapsedMilliseconds);
        return ("standard_import_flow", "LOW", "No specific flow detected, defaulting to standard import");
    }

    private bool ContainsOrFuzzy(string text, string keyword, double threshold)
    {
        // Fast path: exact match
        if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // Fuzzy path: Check if any word in text fuzzy-matches keyword
        var words = text.Split([' ', ',', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var matches = _fuzzyMatcher.FindAllMatches(keyword, words, threshold);
        return matches.Any();
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
