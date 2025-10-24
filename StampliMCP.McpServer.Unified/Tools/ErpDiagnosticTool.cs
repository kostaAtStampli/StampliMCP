using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpDiagnosticTool
{
    [McpServerTool(
        Name = "erp__diagnose_error",
        Title = "ERP Error Diagnostic",
        UseStructuredContent = true)]
    [Description("Diagnose integration errors using ERP-specific diagnostic services if available.")]
    public static async Task<CallToolResult> Execute(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Error message to diagnose")] string errorMessage,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);
        var diagnostic = facade.GetService<IErpDiagnosticService>();

        ErrorDiagnostic result;
        if (diagnostic is null)
        {
            result = new ErrorDiagnostic
            {
                ErrorMessage = errorMessage,
                ErrorCategory = "Unsupported",
                Summary = $"ERP '{erp}' does not provide error diagnostics yet",
                PossibleCauses = new List<string> { "No diagnostic service implemented" },
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=errors",
                        Name = "Search knowledge base"
                    }
                }
            };
        }
        else
        {
            result = await diagnostic.DiagnoseAsync(errorMessage, ct);
            result.Summary ??= $"Diagnostics completed for ERP '{erp}'";
            result.NextActions ??= new List<ResourceLinkBlock>();
        }

        var callResult = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(new { result })
        };

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        callResult.Content.Add(new TextContentBlock { Type = "text", Text = json });
        foreach (var link in result.NextActions)
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }
}
