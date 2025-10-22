using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Acumatica-specific flow service
/// Inherits generic flow loading from base class
/// Adds Acumatica-specific flow matching logic
/// </summary>
public sealed class AcumaticaFlowService : FlowServiceBase
{
    protected override string FlowResourcePrefix => "StampliMCP.McpServer.Acumatica.Module.Knowledge.flows";

    public AcumaticaFlowService(
        ILogger<AcumaticaFlowService> logger,
        IMemoryCache cache,
        FuzzyMatchingService fuzzyMatcher)
        : base(logger, cache, fuzzyMatcher, typeof(AcumaticaFlowService).Assembly)
    {
    }

    /// <summary>
    /// Acumatica-specific flow matching based on description keywords
    /// </summary>
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
            Logger.LogInformation("FlowMatch: vendor_export_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("vendor_export_flow", "HIGH", "User wants to export/create vendor");
        }

        if ((ContainsOrFuzzy(lower, "bill", 0.60) || ContainsOrFuzzy(lower, "invoice", 0.60)) &&
            (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: export_invoice_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("export_invoice_flow", "HIGH", "User wants to export bill/invoice");
        }

        if (ContainsOrFuzzy(lower, "payment", 0.60) && (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: payment_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("payment_flow", "HIGH", "User wants to export payment");
        }

        if (ContainsOrFuzzy(lower, "purchase order", 0.60) && (ContainsOrFuzzy(lower, "export", 0.60) || ContainsOrFuzzy(lower, "create", 0.60)))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: export_po_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("export_po_flow", "HIGH", "User wants to export purchase order");
        }

        // PO matching flows
        if (ContainsOrFuzzy(lower, "po matching", 0.60) && (lower.Contains("all") || lower.Contains("closed")))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: po_matching_full_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("po_matching_full_import_flow", "HIGH", "User wants full PO matching with closed data");
        }

        if (ContainsOrFuzzy(lower, "po matching", 0.60) || (ContainsOrFuzzy(lower, "purchase order", 0.60) && lower.Contains("match")))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: po_matching_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("po_matching_flow", "MEDIUM", "User wants single PO matching");
        }

        // M2M flow
        if (lower.Contains("m2m") || lower.Contains("many to many") || lower.Contains("relationship") ||
            (lower.Contains("branch") && lower.Contains("project")) ||
            (lower.Contains("project") && lower.Contains("task")) ||
            (lower.Contains("task") && lower.Contains("cost code")))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: m2m_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("m2m_import_flow", "HIGH", "User wants M2M relationship import");
        }

        // API actions
        if (lower.Contains("void") || lower.Contains("release") || lower.Contains("action"))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: api_action_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("api_action_flow", "MEDIUM", "User wants to perform API action (void/release)");
        }

        // Default: Standard import (most common)
        if (ContainsOrFuzzy(lower, "import", 0.60) || lower.Contains("get") || lower.Contains("retrieve") ||
            lower.Contains("fetch") || lower.Contains("custom field") || ContainsOrFuzzy(lower, "vendor", 0.60) ||
            lower.Contains("account") || ContainsOrFuzzy(lower, "item", 0.60) || lower.Contains("tax"))
        {
            sw.Stop();
            Logger.LogInformation("FlowMatch: standard_import_flow, time={Ms}ms", sw.ElapsedMilliseconds);
            return ("standard_import_flow", "HIGH", "User wants to import data using standard pagination pattern");
        }

        // Fallback
        sw.Stop();
        Logger.LogInformation("FlowMatch: standard_import_flow (fallback), time={Ms}ms", sw.ElapsedMilliseconds);
        return ("standard_import_flow", "LOW", "No specific flow detected, defaulting to standard import");
    }
}
