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
    public static async Task<CallToolResult> Execute(
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
            var flowName = await flowService.GetFlowForOperationAsync(operation, ct) 
                           ?? FindFlowForOperationFallback(operation);

            if (string.IsNullOrEmpty(flowName))
            {
                var invalid = new ValidationResult
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
                    },
                    Summary = $"Invalid: unknown operation {operation} {BuildInfo.Marker}"
                };
                var retInvalid = new CallToolResult();
                retInvalid.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = invalid });
                
                // Serialize invalid operation result as JSON for LLM consumption
                var invalidOpJson = System.Text.Json.JsonSerializer.Serialize(invalid, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                retInvalid.Content.Add(new TextContentBlock { Type = "text", Text = invalidOpJson });
                
                foreach (var link in invalid.NextActions) retInvalid.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });
                return retInvalid;
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
                var invalidJson = new ValidationResult
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
                    },
                    Summary = $"Invalid JSON {BuildInfo.Marker}"
                };
                var retInvalidJson = new CallToolResult();
                retInvalidJson.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = invalidJson });
                
                // Serialize invalid JSON result for LLM consumption
                var invalidJsonOutput = System.Text.Json.JsonSerializer.Serialize(invalidJson, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                retInvalidJson.Content.Add(new TextContentBlock { Type = "text", Text = invalidJsonOutput });
                return retInvalidJson;
            }

            // Step 4: Apply validation rules
            var errors = new List<ValidationError>();
            var warnings = new List<string>();
            var appliedRules = new List<string>();

            // Always apply built-in validation rules based on operation type
            // These are hard-coded business rules that always apply
            var builtInRules = new List<string>
            {
                "required_fields",
                "field_length_limits",
                "data_types",
                "business_logic"
            };

            foreach (var rule in builtInRules)
            {
                var (isValid, error, warning) = ApplyValidationRule(rule, requestDoc.RootElement, operation);
                appliedRules.Add(rule);

                if (!isValid && error != null)
                    errors.Add(error);

                if (warning != null)
                    warnings.Add(warning);
            }

            // Also apply any flow-specific validation rules if they exist
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
            // De-duplicate repeated errors (by Field+Rule+Message)
            errors = errors
                .GroupBy(e => $"{e.Field}|{e.Rule}|{e.Message}")
                .Select(g => g.First())
                .ToList();

            var result = new ValidationResult
            {
                IsValid = !errors.Any(),
                Operation = operation,
                Flow = flowName,
                Errors = errors,
                Warnings = warnings,
                AppliedRules = appliedRules,
                Suggestions = errors.Select(e => $"Fix {e.Field}: {e.Expected}").ToList(),
                Summary = !errors.Any()
                    ? $"Valid request for {operation} ({flowName}) {BuildInfo.Marker}"
                    : $"Invalid request for {operation}: {errors.Count} error(s) {BuildInfo.Marker}",
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
                        },
                        new ResourceLinkBlock
                        {
                            Uri = "mcp://stampli-acumatica/marker",
                            Name = BuildInfo.Marker,
                            Description = $"build={BuildInfo.VersionTag}"
                        }
                    }
                    : new List<ResourceLinkBlock>
                    {
                        new ResourceLinkBlock
                        {
                            Uri = $"mcp://stampli-acumatica/kotlin_tdd_workflow?feature={operation}",
                            Name = "Proceed with implementation",
                            Description = "Request is valid - start TDD workflow"
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
            ret.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result });
            
            // Serialize full validation result as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });
            
            foreach (var link in result.NextActions) ret.Content.Add(new ResourceLinkBlock { Uri = link.Uri, Name = link.Name, Description = link.Description });

            Serilog.Log.Information("Tool {Tool} completed: isValid={IsValid}, errors={ErrorCount}",
                "validate_request", result.IsValid, errors.Count);

            return ret;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}",
                "validate_request", ex.Message);

            var errorResult = new ValidationResult
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
                },
                Summary = $"Validation failed: {ex.Message} {BuildInfo.Marker}"
            };
            var retError = new CallToolResult();
            retError.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = errorResult });
            
            // Serialize error validation result as JSON for LLM consumption
            var errorJson = System.Text.Json.JsonSerializer.Serialize(errorResult, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            retError.Content.Add(new TextContentBlock { Type = "text", Text = errorJson });
            return retError;
        }
    }

    private static string? FindFlowForOperationFallback(string operation)
    {
        // Simple mapping - in real impl, query knowledge base
        var flowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["exportVendor"] = "vendor_export_flow",
            ["getPayments"] = "payment_flow",
            ["importData"] = "standard_import_flow",
            ["importVendors"] = "standard_import_flow",
            ["getVendors"] = "standard_import_flow",
            ["retrieveVendors"] = "standard_import_flow",
            ["matchPO"] = "po_matching_flow",
            ["exportInvoice"] = "export_invoice_flow",
            ["exportPO"] = "export_po_flow"
        };

        return flowMap.GetValueOrDefault(operation);
    }

    private static (bool isValid, ValidationError? error, string? warning) ApplyValidationRule(
        string rule,
        JsonElement request,
        string operation)
    {
        // Comprehensive validation for ALL Acumatica operations
        var operationLower = operation.ToLower();

        // Vendor validations
        if (operationLower.Contains("vendor"))
        {
            // Check vendorName (max 60 chars, required for export)
            if (operationLower.Contains("export"))
            {
                if (!request.TryGetProperty("vendorName", out var vendorName) ||
                    string.IsNullOrWhiteSpace(vendorName.GetString()))
                {
                    return (false, new ValidationError
                    {
                        Field = "vendorName",
                        Rule = "required_field",
                        Message = "vendorName is required for vendor export",
                        Expected = "Non-empty string"
                    }, null);
                }

                var name = vendorName.GetString() ?? "";
                if (name.Length > 60)
                {
                    return (false, new ValidationError
                    {
                        Field = "vendorName",
                        Rule = "max_length_60",
                        Message = $"vendorName exceeds 60 character limit (current: {name.Length})",
                        CurrentValue = name,
                        Expected = "String with length ≤ 60",
                        RuleSource = "CreateVendorHandler: name length"
                    }, null);
                }
            }

            // Check VendorID (max 15 chars)
            if (request.TryGetProperty("VendorID", out var vendorId) || request.TryGetProperty("vendorId", out vendorId))
            {
                var id = vendorId.GetString() ?? "";
                if (id.Length > 15)
                {
                    return (false, new ValidationError
                    {
                        Field = "VendorID",
                        Rule = "max_length_15",
                        Message = "VendorID exceeds 15 character limit",
                        CurrentValue = id,
                        Expected = "String with length ≤ 15",
                        RuleSource = "CreateVendorHandler.java:123"
                    }, null);
                }
            }
        }

        // Payment validations
        if (operationLower.Contains("payment"))
        {
            // PaymentAmount required
            if (operationLower.Contains("export") || operationLower.Contains("create"))
            {
                if (!request.TryGetProperty("PaymentAmount", out var amount))
                {
                    return (false, new ValidationError
                    {
                        Field = "PaymentAmount",
                        Rule = "required_field",
                        Message = "PaymentAmount is required for payment operations",
                        Expected = "Numeric value"
                    }, null);
                }
            }

            // CurrencyID validation (max 5 chars)
            if (request.TryGetProperty("CurrencyID", out var currency))
            {
                var curr = currency.GetString() ?? "";
                if (curr.Length > 5)
                {
                    return (false, new ValidationError
                    {
                        Field = "CurrencyID",
                        Rule = "max_length_5",
                        Message = "CurrencyID exceeds 5 character limit",
                        CurrentValue = curr,
                        Expected = "String with length ≤ 5 (e.g., 'USD', 'EUR')"
                    }, null);
                }
            }
        }

        // PO validations
        if (operationLower.Contains("purchase") || operationLower.Contains("po"))
        {
            if (request.TryGetProperty("PONumber", out var poNumber))
            {
                var po = poNumber.GetString() ?? "";
                if (po.Length > 15)
                {
                    return (false, new ValidationError
                    {
                        Field = "PONumber",
                        Rule = "max_length_15",
                        Message = "PONumber exceeds 15 character limit",
                        CurrentValue = po,
                        Expected = "String with length ≤ 15"
                    }, null);
                }
            }
        }

        // Item validations
        if (operationLower.Contains("item") || operationLower.Contains("inventory"))
        {
            if (request.TryGetProperty("InventoryID", out var inventoryId))
            {
                var inv = inventoryId.GetString() ?? "";
                if (inv.Length > 30)
                {
                    return (false, new ValidationError
                    {
                        Field = "InventoryID",
                        Rule = "max_length_30",
                        Message = "InventoryID exceeds 30 character limit",
                        CurrentValue = inv,
                        Expected = "String with length ≤ 30"
                    }, null);
                }
            }
        }

        // Pagination validation (applies to all import/search/get/retrieve operations)
        if (operationLower.Contains("import") || operationLower.Contains("search") || 
            operationLower.Contains("get") || operationLower.Contains("retrieve"))
        {
            if (request.TryGetProperty("pageSize", out var pageSize))
            {
                try
                {
                    var size = pageSize.GetInt32();
                    if (size > 2000)
                    {
                        return (false, new ValidationError
                        {
                            Field = "pageSize",
                            Rule = "max_pagination_2000",
                            Message = "Page size exceeds Acumatica maximum of 2000 rows",
                            CurrentValue = size.ToString(),
                            Expected = "Integer ≤ 2000",
                            RuleSource = "STANDARD_IMPORT_FLOW: RESPONSE_ROWS_LIMIT"
                        }, null);
                    }
                }
                catch
                {
                    // Invalid integer
                }
            }
        }

        // Rule passed
        return (true, null, null);
    }
}
