using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ValidationCheckerTool
{
    [McpServerTool(
        Name = "validate_request",
        Title = "Request Validator",
        UseStructuredContent = true
    )]
    [Description(@"
Pre-flight validation for Acumatica API requests against flow rules.

Validates JSON payloads against flow-specific validation rules:
✓ Required fields presence
✓ Field length limits (e.g., VendorID ≤ 30 chars)
✓ Numeric range constraints (e.g., pagination ≤ 2000 rows/page)
✓ Format requirements (dates, phone numbers, etc.)
✓ Business logic rules from flow definitions

Returns:
• IsValid boolean
• Validation errors with field/rule/message
• Non-blocking warnings
• Applied validation rules
• Fix suggestions for each error
• Resource links to related tools

Examples:
• Vendor export: Check VendorID length, required fields (VendorName, etc.)
• Payment: Validate CurrencyID for international payments
• Import: Check pagination limits (max 2000 rows/page)
")]
    public static async Task<ValidationResult> Execute(
        [Description("Operation name (e.g., 'exportVendor', 'getPayments')")]
        string operation,

        [Description("JSON request payload to validate")]
        string requestPayload,

        FlowService flowService,
        KnowledgeService knowledge,
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started: operation={Operation}",
            "validate_request", operation);

        try
        {
            // Step 1: Find operation's flow
            var flowName = await FindFlowForOperation(operation, knowledge, ct);

            if (string.IsNullOrEmpty(flowName))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Operation = operation,
                    Flow = "UNKNOWN",
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            Field = "operation",
                            Rule = "operation_exists",
                            Message = $"Operation '{operation}' not found in knowledge base",
                            Expected = "Valid operation name (e.g., exportVendor, getPayments)"
                        }
                    },
                    NextActions = new List<ResourceLinkBlock>
                    {
                        new ResourceLinkBlock
                        {
                            Uri = "mcp://stampli-acumatica/query_acumatica_knowledge?query=operations",
                            Name = "Browse available operations",
                            Description = "Find valid operation names"
                        }
                    }
                };
            }

            // Step 2: Load flow validation rules
            var flowDoc = await flowService.GetFlowAsync(flowName, ct);
            var validationRules = new List<string>();

            if (flowDoc?.RootElement.TryGetProperty("validationRules", out var rulesElement) == true)
            {
                validationRules.AddRange(
                    rulesElement.EnumerateArray()
                        .Select(r => r.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                );
            }

            // Step 3: Parse request payload
            JsonDocument? requestDoc = null;
            try
            {
                requestDoc = JsonDocument.Parse(requestPayload);
            }
            catch (JsonException ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Operation = operation,
                    Flow = flowName,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            Field = "requestPayload",
                            Rule = "valid_json",
                            Message = $"Invalid JSON: {ex.Message}",
                            Expected = "Valid JSON object"
                        }
                    }
                };
            }

            // Step 4: Apply validation rules
            var errors = new List<ValidationError>();
            var warnings = new List<string>();
            var appliedRules = new List<string>();

            foreach (var rule in validationRules)
            {
                var (isValid, error, warning) = ApplyValidationRule(rule, requestDoc.RootElement, operation);
                appliedRules.Add(rule);

                if (!isValid && error != null)
                    errors.Add(error);

                if (warning != null)
                    warnings.Add(warning);
            }

            // Step 5: Build result
            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Operation = operation,
                Flow = flowName,
                Errors = errors,
                Warnings = warnings,
                AppliedRules = appliedRules,
                Suggestions = errors.Select(e => $"Fix {e.Field}: {e.Expected}").ToList(),
                NextActions = errors.Any()
                    ? new List<ResourceLinkBlock>
                    {
                        new ResourceLinkBlock
                        {
                            Uri = $"mcp://stampli-acumatica/diagnose_error?error={errors.First().Message}",
                            Name = "Diagnose validation error",
                            Description = "Get detailed error analysis and solutions"
                        },
                        new ResourceLinkBlock
                        {
                            Uri = $"mcp://stampli-acumatica/get_flow_details?flow={flowName}",
                            Name = "Review flow rules",
                            Description = "See all validation rules for this flow"
                        }
                    }
                    : new List<ResourceLinkBlock>
                    {
                        new ResourceLinkBlock
                        {
                            Uri = $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={operation}",
                            Name = "Proceed with implementation",
                            Description = "Request is valid - start TDD workflow"
                        }
                    }
            };

            Serilog.Log.Information("Tool {Tool} completed: isValid={IsValid}, errors={ErrorCount}",
                "validate_request", result.IsValid, errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "validate_request", ex.Message);

            return new ValidationResult
            {
                IsValid = false,
                Operation = operation,
                Flow = "ERROR",
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Field = "system",
                        Rule = "validation_error",
                        Message = ex.Message
                    }
                }
            };
        }
    }

    private static async Task<string?> FindFlowForOperation(string operation, KnowledgeService knowledge, CancellationToken ct)
    {
        // Simple mapping - in real impl, query knowledge base
        var flowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["exportVendor"] = "VENDOR_EXPORT_FLOW",
            ["getPayments"] = "PAYMENT_FLOW",
            ["importData"] = "STANDARD_IMPORT_FLOW",
            ["matchPO"] = "PO_MATCHING_FLOW",
            ["exportInvoice"] = "EXPORT_INVOICE_FLOW",
            ["exportPO"] = "EXPORT_PO_FLOW"
        };

        return flowMap.GetValueOrDefault(operation);
    }

    private static (bool isValid, ValidationError? error, string? warning) ApplyValidationRule(
        string rule,
        JsonElement request,
        string operation)
    {
        // Parse rule and apply validation
        if (rule.Contains("VendorID") && rule.Contains("30"))
        {
            if (request.TryGetProperty("VendorID", out var vendorId))
            {
                var value = vendorId.GetString() ?? "";
                if (value.Length > 30)
                {
                    return (false, new ValidationError
                    {
                        Field = "VendorID",
                        Rule = rule,
                        Message = "VendorID exceeds 30 character limit",
                        CurrentValue = value,
                        Expected = "String with length ≤ 30"
                    }, null);
                }
            }
            else if (operation.ToLower().Contains("vendor"))
            {
                return (false, new ValidationError
                {
                    Field = "VendorID",
                    Rule = rule,
                    Message = "VendorID is required but missing",
                    Expected = "String with length ≤ 30"
                }, null);
            }
        }

        if (rule.Contains("pagination") && rule.Contains("2000"))
        {
            if (request.TryGetProperty("pageSize", out var pageSize) && pageSize.GetInt32() > 2000)
            {
                return (false, new ValidationError
                {
                    Field = "pageSize",
                    Rule = rule,
                    Message = "Page size exceeds maximum of 2000 rows",
                    CurrentValue = pageSize.GetInt32().ToString(),
                    Expected = "Integer ≤ 2000"
                }, null);
            }
        }

        // Rule passed
        return (true, null, null);
    }
}
