using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class FlowDetailsTool
{
    [McpServerTool(
        Name = "get_flow_details",
        Title = "Flow Details Lookup",
        UseStructuredContent = true
    )]
    [Description(@"
Get complete details for a specific Acumatica integration flow.

Returns:
✓ Flow anatomy (step-by-step structure)
✓ Constants (pagination limits, max lengths, defaults)
✓ Validation rules (required fields, max values, formats)
✓ Code snippets from actual implementation
✓ Critical files to review with line ranges
✓ Resource links to TDD workflow and related tools

Examples:
- VENDOR_EXPORT_FLOW → Validation + JSON mapping + UI link generation
- PAYMENT_FLOW → International payment with cross-rate calculation
- STANDARD_IMPORT_FLOW → Pagination (2000 rows/page) + auth wrapper

Available flows:
VENDOR_EXPORT_FLOW, PAYMENT_FLOW, STANDARD_IMPORT_FLOW, PO_MATCHING_FLOW,
PO_MATCHING_FULL_IMPORT_FLOW, EXPORT_INVOICE_FLOW, EXPORT_PO_FLOW,
M2M_IMPORT_FLOW, API_ACTION_FLOW
")]
    public static async Task<CallToolResult> Execute(
        [Description("Flow name (e.g., VENDOR_EXPORT_FLOW, PAYMENT_FLOW, STANDARD_IMPORT_FLOW)")]
        string flowName,

        FlowService flowService,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: flowName={FlowName}",
            "get_flow_details", flowName);

        try
        {
            var flowDoc = await flowService.GetFlowAsync(flowName, ct);

            if (flowDoc == null)
            {
                Serilog.Log.Warning("Flow {FlowName} not found", flowName);
                var ret404 = new CallToolResult();
                var detail404 = new FlowDetail
                {
                    Name = flowName,
                    Description = $"Flow '{flowName}' not found. Check flow name spelling or use query_acumatica_knowledge to search.",
                    NextActions = new List<ResourceLinkBlock>
                    {
                        new ResourceLinkBlock
                        {
                            Uri = "mcp://stampli-acumatica/query_acumatica_knowledge?query=flows",
                            Name = "List all available flows"
                        }
                    }
                };
                ret404.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = detail404 });
                ret404.Content.Add(new TextContentBlock { Type = "text", Text = $"Flow not found: {flowName} {BuildInfo.Marker}" });
                foreach (var link in detail404.NextActions) ret404.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
                return ret404;
            }

            var flow = flowDoc.RootElement;

            // Parse flow anatomy
            var anatomy = new FlowAnatomy();
            if (flow.TryGetProperty("anatomy", out var anatomyProp))
            {
                anatomy.Flow = anatomyProp.TryGetProperty("flow", out var flowDesc)
                    ? flowDesc.GetString() ?? ""
                    : "";

                anatomy.Validation = anatomyProp.TryGetProperty("validation", out var val)
                    ? val.GetString()
                    : null;

                anatomy.Mapping = anatomyProp.TryGetProperty("mapping", out var map)
                    ? map.GetString()
                    : null;

                // Additional fields
                var additionalInfo = new Dictionary<string, string>();
                foreach (var prop in anatomyProp.EnumerateObject())
                {
                    if (prop.Name != "flow" && prop.Name != "validation" && prop.Name != "mapping")
                    {
                        additionalInfo[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
                anatomy.AdditionalInfo = additionalInfo;
            }

            // Parse constants
            var constants = new Dictionary<string, ConstantInfo>();
            if (flow.TryGetProperty("constants", out var constantsProp))
            {
                foreach (var constant in constantsProp.EnumerateObject())
                {
                    var constObj = constant.Value;
                    constants[constant.Name] = new ConstantInfo
                    {
                        Name = constant.Name,
                        Value = constObj.TryGetProperty("value", out var val) ? val.ToString() : "",
                        File = constObj.TryGetProperty("file", out var file) ? file.GetString() : null,
                        Line = constObj.TryGetProperty("line", out var line) ? line.GetInt32() : null,
                        Purpose = constObj.TryGetProperty("purpose", out var purpose) ? purpose.GetString() : null
                    };
                }
            }

            // Parse validation rules
            var validationRules = new List<string>();
            if (flow.TryGetProperty("validationRules", out var rulesProp))
            {
                validationRules.AddRange(
                    rulesProp.EnumerateArray().Select(r => r.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s))
                );
            }

            // Parse code snippets
            var codeSnippets = new Dictionary<string, string>();
            if (flow.TryGetProperty("codeSnippets", out var snippetsProp))
            {
                foreach (var snippet in snippetsProp.EnumerateObject())
                {
                    codeSnippets[snippet.Name] = snippet.Value.GetString() ?? "";
                }
            }

            // Parse critical files
            var criticalFiles = new List<FileReference>();
            if (flow.TryGetProperty("criticalFiles", out var filesProp))
            {
                foreach (var fileElement in filesProp.EnumerateArray())
                {
                    var fileRef = new FileReference
                    {
                        File = fileElement.TryGetProperty("file", out var f) ? f.GetString() ?? "" : "",
                        Lines = fileElement.TryGetProperty("lines", out var l) ? l.GetString() : null,
                        Purpose = fileElement.TryGetProperty("purpose", out var p) ? p.GetString() : null,
                        KeyPatterns = fileElement.TryGetProperty("keyPatterns", out var kp)
                            ? kp.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList()
                            : new List<string>()
                    };
                    criticalFiles.Add(fileRef);
                }
            }

            // Get operations that use this flow
            var usedByOperations = flow.GetProperty("usedByOperations")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            var structured = new FlowDetail
            {
                Name = flowName,
                Description = flow.GetProperty("description").GetString() ?? "",
                Anatomy = anatomy,
                Constants = constants,
                ValidationRules = validationRules,
                CodeSnippets = codeSnippets,
                CriticalFiles = criticalFiles,
                Summary = $"{flowName}: constants={constants.Count}, rules={validationRules.Count} {BuildInfo.Marker}",
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = usedByOperations.Any()
                            ? $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={usedByOperations.First()}"
                            : $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={flowName}",
                        Name = usedByOperations.Any()
                            ? $"Generate TDD workflow for {usedByOperations.First()}"
                            : "Generate TDD workflow"
                    },
                    new ResourceLinkBlock
                    {
                        Uri = $"mcp://stampli-acumatica/query_acumatica_knowledge?query={flowName}",
                        Name = "Search related knowledge"
                    },
                    new ResourceLinkBlock
                    {
                        Uri = "mcp://stampli-acumatica/marker",
                        Name = BuildInfo.Marker,
                        Description = $"build={BuildInfo.VersionTag}"
                    }
                }
            };

            var ret = new CallToolResult();
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = structured });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = structured.Summary ?? $"{flowName} {BuildInfo.Marker}" });
            foreach (var link in structured.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });

            Serilog.Log.Information("Tool {Tool} completed: flow={Flow}, constants={ConstCount}, rules={RuleCount}",
                "get_flow_details", flowName, constants.Count, validationRules.Count);

            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: flowName={FlowName}, error={Error}",
                "get_flow_details", flowName, ex.Message);

            var ret = new CallToolResult();
            var detail = new FlowDetail
            {
                Name = flowName,
                Description = $"Error loading flow: {ex.Message}",
                NextActions = new List<ResourceLinkBlock>
                {
                    new ResourceLinkBlock
                    {
                        Uri = "mcp://stampli-acumatica/health_check",
                        Name = "Check server health"
                    }
                }
            };
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = detail });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = $"error: {ex.Message} {BuildInfo.Marker}" });
            foreach (var link in detail.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
            return ret;
        }
    }
}
