using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        IMcpServer server,
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

        if (!result.IsValid)
        {
            var missingFields = ExtractMissingFields(result.Errors);
            if (missingFields.Count > 0)
            {
                var fields = new[]
                {
                    new Services.ElicitationCompat.Field(
                        Name: "autoFix",
                        Kind: "boolean",
                        Description: $"Autofill placeholders for: {string.Join(", ", missingFields)}?")
                };

                var outcome = await Services.ElicitationCompat.TryElicitAsync(
                    server,
                    "Missing required fields detected. Autofill placeholder values?",
                    fields,
                    ct);

                if (outcome.Supported && string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase) &&
                    outcome.Content is { } content && content.TryGetValue("autoFix", out var autoFixElement) &&
                    autoFixElement.ValueKind == JsonValueKind.True)
                {
                    var patched = TryBuildPatchedPayload(requestPayload, missingFields);
                    if (patched is not null)
                    {
                        result.SuggestedPayload = patched;
                        result.Suggestions ??= new List<string>();
                        result.Suggestions.Add("SuggestedPayload contains placeholder values for missing fields. Replace and revalidate.");
                    }
                }
            }
        }


        result.Summary = result.IsValid
            ? $"Valid request for {operation} ({result.Flow})"
            : $"Invalid request for {operation}: {result.Errors.Count} error(s)";

        result.NextActions = result.IsValid
            ? new List<ResourceLinkBlock>
            {
                new ResourceLinkBlock
                {
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query=*&scope=operations",
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
                    Uri = $"mcp://stampli-unified/erp__query_knowledge?erp={erp}&query={Uri.EscapeDataString(result.Flow ?? string.Empty)}&scope=flows",
                    Name = "Review flow rules"
                }
            };

        if (!result.IsValid && result.SuggestedPayload is not null)
        {
            result.NextActions.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/erp__validate_request",
                Name = "Re-run validation with SuggestedPayload",
                Description = "Copy SuggestedPayload into payload before calling again."
            });
        }

        return BuildCallToolResult(result);
    }

    private static List<string> ExtractMissingFields(IEnumerable<ValidationError> errors)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var error in errors ?? Enumerable.Empty<ValidationError>())
        {
            if (error.Rule is not ("required_fields" or "flow_required_field"))
            {
                continue;
            }

            var parts = (error.Field ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    fields.Add(part.Trim());
                }
            }
        }

        return fields.ToList();
    }

    private static string? TryBuildPatchedPayload(string payload, IEnumerable<string> missingFields)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            var node = JsonNode.Parse(payload);
            if (node is not JsonObject obj)
            {
                return null;
            }

            foreach (var field in missingFields)
            {
                if (string.IsNullOrWhiteSpace(field))
                {
                    continue;
                }

                var property = field.Trim();
                if (!obj.ContainsKey(property))
                {
                    obj[property] = $"<TODO: provide {property}>";
                }
            }

            return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return null;
        }
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

        var instructionText = ToolLinkFormatter.BuildInstructionList(result.NextActions);
        if (!string.IsNullOrWhiteSpace(instructionText))
        {
            callResult.Content.Add(new TextContentBlock { Type = "text", Text = instructionText });
        }

        foreach (var link in result.NextActions ?? Enumerable.Empty<ResourceLinkBlock>())
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }
}
