using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpValidationTool
{
    [McpServerTool(
        Name = "erp__validate_request",
        Title = "ERP Request Validator",
        UseStructuredContent = true)]
    [Description("Validate request payloads using ERP-specific validation services.")]
    public static async Task<CallToolResult> Execute(
        [Description("ERP identifier (e.g., acumatica)")] string erp,
        [Description("Operation name (e.g., exportVendor)")] string operation,
        [Description("JSON request payload to validate")] string requestPayload,
        ErpRegistry registry,
        CancellationToken ct)
    {
        using var facade = registry.GetFacade(erp);
        var validation = facade.GetService<IErpValidationService>();

        if (validation is null)
        {
            var unsupported = new ValidationResult
            {
                IsValid = false,
                Operation = operation,
                Flow = "UNSUPPORTED",
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Field = "operation",
                        Rule = "validation_not_available",
                        Message = $"ERP '{erp}' does not provide validation services yet"
                    }
                },
                Summary = $"Validation not available for ERP '{erp}'",
                NextActions = new List<ResourceLinkBlock>
                {
                    new()
                    {
                        Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=validation",
                        Name = "Search knowledge base"
                    }
                }
            };

            return BuildCallToolResult(unsupported);
        }

        var result = await validation.ValidateAsync(operation, requestPayload, ct);

        result.Summary = result.IsValid
            ? $"Valid request for {operation} ({result.Flow})"
            : $"Invalid request for {operation}: {result.Errors.Count} error(s)";

        result.NextActions = result.IsValid
            ? new List<ResourceLinkBlock>
            {
                new ResourceLinkBlock
                {
                    Uri = $"mcp://stampli-unified/erp__list_operations?erp={erp}",
                    Name = "Review operations"
                }
            }
            : new List<ResourceLinkBlock>
            {
                new ResourceLinkBlock
                {
                    Uri = $"mcp://stampli-unified/erp__diagnose_error?erp={erp}&error={Uri.EscapeDataString(result.Errors.First().Message)}",
                    Name = "Diagnose validation error"
                },
                new ResourceLinkBlock
                {
                    Uri = $"mcp://stampli-unified/erp__get_flow_details?erp={erp}&flow={result.Flow}",
                    Name = "Review flow rules"
                }
            };

        return BuildCallToolResult(result);
    }

    private static CallToolResult BuildCallToolResult(ValidationResult result)
    {
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
        foreach (var link in result.NextActions ?? Enumerable.Empty<ResourceLinkBlock>())
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }
}
