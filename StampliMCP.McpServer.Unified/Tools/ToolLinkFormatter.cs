using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModelContextProtocol.Protocol;

namespace StampliMCP.McpServer.Unified.Tools;

internal static class ToolLinkFormatter
{
    private const string Prefix = "mcp://stampli-unified/";

    internal static string BuildInstructionList(IEnumerable<ResourceLinkBlock>? links)
    {
        if (links is null)
        {
            return string.Empty;
        }

        var commands = links
            .Select(link => TryFormatToolCall(link.Uri))
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (commands.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("Tools to run:\n");
        for (var i = 0; i < commands.Count; i++)
        {
            builder.Append("- ");
            builder.Append(commands[i]);
            if (i < commands.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string? TryFormatToolCall(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = uri[Prefix.Length..];
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        var parts = remainder.Split('?', 2);
        var toolName = NormalizeToolName(parts[0].Trim('/'));
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        if (parts.Length == 1 || string.IsNullOrWhiteSpace(parts[1]))
        {
            return $"tools/call name={toolName}";
        }

        var assignments = new List<string>();
        foreach (var pair in parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            assignments.Add($"\"{key}\": \"{value}\"");
        }

        var arguments = assignments.Count == 0
            ? string.Empty
            : $" arguments={{ {string.Join(", ", assignments)} }}";

        return $"tools/call name={toolName}{arguments}";
    }

    private static string NormalizeToolName(string raw)
    {
        return raw switch
        {
            "erpquery_knowledge" => "erp__query_knowledge",
            "erprecommend_flow" => "erp__recommend_flow",
            "erpvalidate_request" => "erp__validate_request",
            "erpdiagnose_error" => "erp__diagnose_error",
            "erphealth_check" => "erp__health_check",
            "erpknowledge_update_plan" => "erp__knowledge_update_plan",
            _ => raw
        };
    }
}
