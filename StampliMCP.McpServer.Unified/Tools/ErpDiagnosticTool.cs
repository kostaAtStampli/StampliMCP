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
        IMcpServer server,
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

            var needsClarification = (result.Solutions is null || result.Solutions.Count == 0) || result.PossibleCauses.Count > 1;
            if (needsClarification)
            {
                var fields = new[]
                {
                    new Services.ElicitationCompat.Field(
                        Name: "operation",
                        Kind: "string",
                        Description: "Which operation or entity triggered this error?"),
                    new Services.ElicitationCompat.Field(
                        Name: "stage",
                        Kind: "string",
                        Description: "Workflow stage (import, export, validation, action, etc.)"),
                    new Services.ElicitationCompat.Field(
                        Name: "recentChanges",
                        Kind: "boolean",
                        Description: "Did this start right after a configuration or code change?")
                };

                var outcome = await Services.ElicitationCompat.TryElicitAsync(
                    server,
                    "Add optional context so diagnostics can narrow the root cause.",
                    fields,
                    ct);

                if (outcome.Supported && string.Equals(outcome.Action, "accept", StringComparison.OrdinalIgnoreCase) &&
                    outcome.Content is { } content)
                {
                    result.AdditionalContext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    if (content.TryGetValue("operation", out var opElement) && opElement.ValueKind == JsonValueKind.String)
                    {
                        var op = opElement.GetString();
                        if (!string.IsNullOrWhiteSpace(op))
                        {
                            result.AdditionalContext["operation"] = op;
                        }
                    }

                    if (content.TryGetValue("stage", out var stageElement) && stageElement.ValueKind == JsonValueKind.String)
                    {
                        var stage = stageElement.GetString();
                        if (!string.IsNullOrWhiteSpace(stage))
                        {
                            result.AdditionalContext["stage"] = stage;
                        }
                    }

                    if (content.TryGetValue("recentChanges", out var recentElement) && recentElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        var recent = recentElement.ValueKind == JsonValueKind.True;
                        result.AdditionalContext["recentChanges"] = recent ? "true" : "false";
                        if (recent)
                        {
                            result.PossibleCauses ??= new List<string>();
                            result.PossibleCauses.Add("Recent configuration or code changes may have introduced this regression.");

                            result.PreventionTips ??= new List<string>();
                            result.PreventionTips.Add("Add regression tests or change management review when modifying this flow.");
                        }
                    }
                }
            }
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

        var instructionText = ToolLinkFormatter.BuildInstructionList(result.NextActions);
        if (!string.IsNullOrWhiteSpace(instructionText))
        {
            callResult.Content.Add(new TextContentBlock { Type = "text", Text = instructionText });
        }

        foreach (var link in result.NextActions)
        {
            callResult.Content.Add(link);
        }

        return callResult;
    }
}
