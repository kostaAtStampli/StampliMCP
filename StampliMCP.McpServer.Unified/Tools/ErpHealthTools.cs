using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpHealthTools
{

    [McpServerTool(Name = "erp__health_check", Title = "Unified MCP Health", UseStructuredContent = true)]
    [Description("Provides server health info plus registered ERP summary.")]
    public static CallToolResult Health(ErpRegistry registry)
    {
        var erps = registry.ListErps()
            .Select(d => new
            {
                d.Key,
                d.Aliases,
                Capabilities = ExpandCapabilities(d.Capabilities),
                d.Version,
                d.Description
            })
            .ToList();

        var payload = new
        {
            status = "ok",
            timestamp = DateTimeOffset.UtcNow,
            registeredErps = erps,
            version = "1.0.0-alpha",
            host = Environment.MachineName
        };

        var result = new CallToolResult
        {
            StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = payload })
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        result.Content.Add(new TextContentBlock
        {
            Type = "text",
            Text = json
        });

        return result;
    }

    private static IReadOnlyList<string> ExpandCapabilities(ErpCapability capabilities)
    {
        if (capabilities == ErpCapability.None)
        {
            return Array.Empty<string>();
        }

        var flags = Enum.GetValues<ErpCapability>()
            .Where(flag => flag != ErpCapability.None && capabilities.HasFlag(flag))
            .Select(flag => flag.ToString())
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return flags;
    }
}
