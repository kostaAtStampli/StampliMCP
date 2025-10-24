using System.ComponentModel;
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
        ErpRegistry registry,
        CancellationToken ct)
    {
        // Normalize ERP name (throws if unknown)
        registry.Normalize(erp);

        if (registry.TryGetDescriptor(erp, out var descriptor) && descriptor is not null)
        {
            // Route by ERP until other modules implement their planners
            if (string.Equals(descriptor.Key, "acumatica", StringComparison.OrdinalIgnoreCase))
            {
                var result = await AcuPlanner.Execute(descriptor.Key, prNumber, learnings, currentBranch, dryRun, ct);

                // Tag response with ERP for clients
                result.Content.Insert(0, new TextContentBlock { Type = "text", Text = $"[Unified] ERP = {descriptor.Key}" });
                // Also mirror result.StructuredContent under a wrapper for clients that expect consistent shape
                var wrapped = new
                {
                    erp = descriptor.Key,
                    result = result.StructuredContent
                };
                result.StructuredContent = JsonSerializer.SerializeToNode(wrapped);
                return result;
            }

            // Default unsupported pathway for other ERPs
            var unsupported = new
            {
                erp = descriptor.Key,
                status = "UNSUPPORTED",
                message = $"ERP '{descriptor.Key}' has no knowledge planner yet",
                nextActions = new[]
                {
                    new ResourceLinkBlock { Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={descriptor.Key}&query=*", Name = "Browse knowledge" }
                }
            };

            return new CallToolResult
            {
                StructuredContent = JsonSerializer.SerializeToNode(new { result = unsupported }),
                Content =
                {
                    new TextContentBlock { Type = "text", Text = JsonSerializer.Serialize(unsupported, new JsonSerializerOptions{WriteIndented = true}) }
                }
            };
        }

        // Should not happen due to Normalize(), but keep safe fallback
        throw new KeyNotFoundException($"ERP '{erp}' not registered");
    }
}

