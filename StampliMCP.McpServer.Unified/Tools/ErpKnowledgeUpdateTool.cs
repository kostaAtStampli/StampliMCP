using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;

// Cross-module references
using AcuPlanner = StampliMCP.McpServer.Acumatica.Tools.AddKnowledgeFromPrTool;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpKnowledgeUpdateTool
{
    [McpServerTool(
        Name = "erp__knowledge_update_plan",
        Title = "ERP Knowledge Update Planner",
        UseStructuredContent = true
    )]
    [Description("Plan and optionally apply ERP knowledge updates from PR context with two-scan enforcement (dry-run by default).")]
    public static async Task<CallToolResult> Execute(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Optional PR number (e.g., #456)")] string? prNumber,
        [Description("Key learnings to cross-check against diff (optional)")] string? learnings,
        [Description("Override for git branch; auto-detected when omitted")] string? currentBranch,
        [Description("Set to false to request apply_patch-ready diffs; default true (plan only).")] bool dryRun,
        [Description("Operation mode: plan (default), apply, validate, files")] string? mode,
        ErpRegistry registry,
        CancellationToken ct)
    {
        registry.Normalize(erp);

        if (!registry.TryGetDescriptor(erp, out var descriptor) || descriptor is null)
        {
            throw new KeyNotFoundException($"ERP '{erp}' not registered");
        }

        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "plan" : mode.Trim().ToLowerInvariant();
        var effectiveDryRun = normalizedMode switch
        {
            "apply" => false,
            _ => dryRun
        };

        switch (normalizedMode)
        {
            case "validate":
            {
                var report = await KnowledgeValidationHelper.BuildReportAsync(registry, ct).ConfigureAwait(false);
                return BuildCallToolResult(new
                {
                    erp = descriptor.Key,
                    mode = normalizedMode,
                    summary = new
                    {
                        erpCount = report.Count,
                        successCount = report.Count(r => r.Success),
                        issueCount = report.Sum(r => r.Issues.Count)
                    },
                    entries = report
                });
            }

            case "files":
            {
                var report = KnowledgeFilesHelper.BuildReport(descriptor.Key, registry);
                return BuildCallToolResult(new
                {
                    erp = descriptor.Key,
                    mode = normalizedMode,
                    totalFiles = report.TotalFiles,
                    files = report.Files
                });
            }

            case "plan":
            case "apply":
                break;

            default:
                return BuildCallToolResult(new
                {
                    erp = descriptor.Key,
                    mode = normalizedMode,
                    status = "UNSUPPORTED_MODE",
                    message = $"Mode '{normalizedMode}' is not supported. Use plan, apply, validate, or files."
                });
        }

        // Route by ERP until other modules implement their planners
        if (string.Equals(descriptor.Key, "acumatica", StringComparison.OrdinalIgnoreCase))
        {
            var result = await AcuPlanner.Execute(descriptor.Key, prNumber, learnings, currentBranch, effectiveDryRun, ct).ConfigureAwait(false);

            // Tag response with ERP + mode for clients
            result.Content.Insert(0, new TextContentBlock { Type = "text", Text = $"[Unified] ERP = {descriptor.Key} | mode = {normalizedMode}" });

            var wrapped = new
            {
                erp = descriptor.Key,
                mode = normalizedMode,
                dryRun = effectiveDryRun,
                result = result.StructuredContent
            };
            result.StructuredContent = JsonSerializer.SerializeToNode(wrapped);
            return result;
        }

        var unsupported = new
        {
            erp = descriptor.Key,
            mode = normalizedMode,
            status = "UNSUPPORTED",
            message = $"ERP '{descriptor.Key}' has no knowledge planner yet",
            nextActions = new[]
            {
                new ResourceLinkBlock
                {
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={descriptor.Key}&query=*",
                    Name = "Browse knowledge"
                }
            }
        };

        return BuildCallToolResult(unsupported);
    }

    private static CallToolResult BuildCallToolResult(object payload)
    {
        var wrapper = new { result = payload };
        var node = JsonSerializer.SerializeToNode(wrapper);
        var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });

        return new CallToolResult
        {
            StructuredContent = node,
            Content =
            {
                new TextContentBlock { Type = "text", Text = json }
            }
        };
    }
}
