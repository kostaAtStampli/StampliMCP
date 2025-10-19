using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class McpOverviewTool
{
    [McpServerTool(
        Name = "mcp_overview",
        Title = "MCP Architecture Guide",
        UseStructuredContent = true
    )]
    [Description(@"
START HERE - Comprehensive guide to the Acumatica MCP architecture.

Returns:
✓ 13 available tools and when to use each
✓ 9 integration flows with purposes
✓ 39 operations organized by category
✓ Custom field support matrix (which flows/entities support custom fields)
✓ Typical workflows (TDD, feature add, bug fix, etc.)
✓ Knowledge base structure

Use this tool when:
• New to this MCP and need orientation
• Unsure which tool to use for a task
• Planning a new feature or integration
• Understanding custom field support across entities
")]
    public static Task<CallToolResult> Execute(
        CancellationToken ct
    )
    {
        Serilog.Log.Information("Tool {Tool} started", "mcp_overview");

        try
        {
            var overview = new
            {
                title = "Acumatica MCP Server - Architecture Guide",
                version = BuildInfo.VersionTag,
                marker = BuildInfo.Marker,
                
                tools = new
                {
                    count = 13,
                    categories = new
                    {
                        discovery = new[]
                        {
                            new { name = "mcp_overview", purpose = "START HERE - understand MCP architecture" },
                            new { name = "query_acumatica_knowledge", purpose = "Search 39 operations & 9 flows" },
                            new { name = "list_operations", purpose = "Browse all operations by category" },
                            new { name = "list_flows", purpose = "Browse all integration flows" }
                        },
                        planning = new[]
                        {
                            new { name = "recommend_flow", purpose = "AI-powered flow recommendation" },
                            new { name = "get_flow_details", purpose = "Get anatomy, constants, rules for a flow" },
                            new { name = "kotlin_tdd_workflow", purpose = "Generate TDD task list for new features" }
                        },
                        validation = new[]
                        {
                            new { name = "validate_request", purpose = "Pre-flight validation of API payloads" },
                            new { name = "diagnose_error", purpose = "Intelligent error diagnostics" }
                        },
                        development = new[]
                        {
                            new { name = "get_kotlin_golden_reference", purpose = "Kotlin patterns from exportVendor" },
                            new { name = "challenge_scan_findings", purpose = "Verify accuracy of code scans" },
                            new { name = "health_check", purpose = "Verify MCP server status" }
                        }
                    }
                },
                
                flows = new
                {
                    count = 9,
                    list = new[]
                    {
                        new 
                        { 
                            name = "VENDOR_EXPORT_FLOW",
                            purpose = "Export vendors with validation + UI links",
                            customFieldSupport = "✓ Yes - BAccount.Attribute*",
                            operations = new[] { "exportVendor" }
                        },
                        new 
                        { 
                            name = "EXPORT_INVOICE_FLOW",
                            purpose = "Invoice export with line items",
                            customFieldSupport = "✓ Yes - Document.Attribute* (header), Transactions.Attribute* (lines)",
                            operations = new[] { "exportAPTransaction", "exportInvoice" }
                        },
                        new 
                        { 
                            name = "PAYMENT_FLOW",
                            purpose = "International payments with cross-rate calculation",
                            customFieldSupport = "✓ Yes - Quick Check inverts containers (Transactions.Attribute* for header)",
                            operations = new[] { "exportBillPayment", "exportPaymentReceipt" }
                        },
                        new 
                        { 
                            name = "EXPORT_PO_FLOW",
                            purpose = "Purchase order export",
                            customFieldSupport = "✓ Yes - PurchaseOrder.Attribute* (header), Details.Attribute* (lines)",
                            operations = new[] { "exportPurchaseOrder" }
                        },
                        new 
                        { 
                            name = "STANDARD_IMPORT_FLOW",
                            purpose = "Paginated imports (2000 rows/page)",
                            customFieldSupport = "✗ No - Import operations read standard fields only",
                            operations = new[] { "getVendors", "retrieveVendors", "importPayments", "importPOReceipts" }
                        },
                        new 
                        { 
                            name = "PO_MATCHING_FLOW",
                            purpose = "PO matching with receipt lookup",
                            customFieldSupport = "✗ No - Matching logic only",
                            operations = new[] { "matchPOReceipt" }
                        },
                        new 
                        { 
                            name = "PO_MATCHING_FULL_IMPORT_FLOW",
                            purpose = "Full PO import with line items",
                            customFieldSupport = "✗ No - Import operation",
                            operations = new[] { "importPOContents" }
                        },
                        new 
                        { 
                            name = "M2M_IMPORT_FLOW",
                            purpose = "Many-to-many relationship imports",
                            customFieldSupport = "✗ No - Relationship mapping only",
                            operations = new[] { "importVendorClasses" }
                        },
                        new 
                        { 
                            name = "API_ACTION_FLOW",
                            purpose = "Generic API actions (submit/release/etc.)",
                            customFieldSupport = "✗ No - Action operations only",
                            operations = new[] { "submitPayment", "releasePayment" }
                        }
                    }
                },
                
                operations = new
                {
                    count = 39,
                    categories = new
                    {
                        vendor = new[] { "exportVendor", "getVendors", "retrieveVendors", "importVendorClasses" },
                        payment = new[] { "exportBillPayment", "exportPaymentReceipt", "importPayments", "submitPayment", "releasePayment" },
                        invoice = new[] { "exportAPTransaction", "exportInvoice", "getInvoices" },
                        po = new[] { "exportPurchaseOrder", "importPOReceipts", "importPOContents", "matchPOReceipt" },
                        account = new[] { "getAccounts", "getAccountClasses" },
                        admin = new[] { "testConnection", "getCompanyInfo" }
                    }
                },
                
                customFieldSupport = new
                {
                    summary = "Custom fields supported in EXPORT flows only (4 of 9 flows)",
                    supportedEntities = new object[]
                    {
                        new { entity = "Vendor", dac = "BAccount.Attribute*", flow = "vendor_export_flow", operations = new[] { "exportVendor" } },
                        new { entity = "Invoice", dac = "Document.Attribute* + Transactions.Attribute*", flow = "export_invoice_flow", operations = new[] { "exportAPTransaction", "exportInvoice" } },
                        new { entity = "Payment", dac = "Transactions.Attribute* (header) + Document.Attribute* (lines)", flow = "payment_flow", operations = new[] { "exportBillPayment", "exportPaymentReceipt" }, note = "Quick Check inverts normal container mapping" },
                        new { entity = "PurchaseOrder", dac = "PurchaseOrder.Attribute* + Details.Attribute*", flow = "export_po_flow", operations = new[] { "exportPurchaseOrder" } }
                    },
                    unsupportedEntities = new object[]
                    {
                        new { entity = "Customer", reason = "getCustomerSearchList is stub only, no custom field logic" },
                        new { entity = "SalesOrder", reason = "No operations exist in driver" },
                        new { entity = "Item", reason = "getItemSearchList has no custom field support" }
                    },
                    discoveryPattern = "$adHocSchema - Use GET /entity/{Endpoint}/{Version}/{Entity}/$adHocSchema to discover all custom attributes",
                    configurationScreen = "SM207060 - Click 'Extend Entity' button (even if grayed out)"
                },
                
                typicalWorkflows = new
                {
                    addNewFeature = new[]
                    {
                        "1. Call recommend_flow with use case description",
                        "2. Call get_flow_details for selected flow",
                        "3. Call kotlin_tdd_workflow to generate task list",
                        "4. Call get_kotlin_golden_reference for Kotlin patterns",
                        "5. Implement following TDD: RED → GREEN → REFACTOR",
                        "6. Call validate_request to test payload validation",
                        "7. Call diagnose_error if issues occur"
                    },
                    addTest = new[]
                    {
                        "1. Call query_acumatica_knowledge to find operation",
                        "2. Call get_flow_details to understand validation rules",
                        "3. Review test files in StampliMCP.McpServer.Acumatica.Tests/",
                        "4. Write test using patterns from existing tests",
                        "5. Call validate_request to verify test payloads"
                    },
                    fixBug = new[]
                    {
                        "1. Call diagnose_error with error message",
                        "2. Call query_acumatica_knowledge to find related operations/flows",
                        "3. Review suggested fixes and code snippets",
                        "4. Apply fix to source code",
                        "5. Call validate_request to verify fix",
                        "6. Run tests to ensure no regressions"
                    },
                    customFieldIntegration = new[]
                    {
                        "1. Call query_acumatica_knowledge with 'custom fields {entity}'",
                        "2. Check if entity is in supportedEntities (Vendor/Invoice/Payment/PO)",
                        "3. If supported: Call get_flow_details for entity's flow",
                        "4. Review customFieldSupport section for DAC mapping",
                        "5. Use $adHocSchema endpoint to discover available attributes",
                        "6. Build payload with custom.{DAC}.Attribute* structure",
                        "7. Call validate_request to verify payload",
                        "8. If unsupported: Check unsupportedEntities for reason and workarounds"
                    }
                },
                
                knowledgeBase = new
                {
                    structure = "Knowledge/ directory with auto-discovered embedded resources",
                    files = new
                    {
                        operations = "10 JSON files in operations/ directory (vendor-operations.json, payment-operations.json, etc.)",
                        flows = "9 JSON files in flows/ directory (vendor_export_flow.json, payment_flow.json, etc.)",
                        patterns = "base-classes.json, method-signatures.json, integration-strategy.json",
                        errors = "error-catalog.json with Acumatica error patterns",
                        kotlin = "kotlin/ directory with golden reference patterns",
                        customFields = "custom-field-patterns.json, custom-field-errors.json, custom-field-onboarding.json"
                    },
                    extensibility = "Drop new JSON/MD/XML files in Knowledge/ to auto-extend MCP (no code changes needed)"
                },
                
                nextSteps = new[]
                {
                    "Use query_acumatica_knowledge to search for specific operations or patterns",
                    "Use recommend_flow when planning a new integration",
                    "Use kotlin_tdd_workflow when implementing new features",
                    "Use validate_request before making API calls",
                    "Use diagnose_error when troubleshooting issues"
                }
            };

            var ret = new CallToolResult();
            ret.StructuredContent = JsonSerializer.SerializeToNode(overview);
            
            // Serialize full overview as JSON for LLM consumption
            var jsonOutput = JsonSerializer.Serialize(overview, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            ret.Content.Add(new TextContentBlock { Type = "text", Text = jsonOutput });

            ret.Content.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-acumatica/query_acumatica_knowledge?query=operations&scope=all",
                Name = "Browse all operations",
                Description = "Search 39 operations across 6 categories"
            });

            ret.Content.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-acumatica/list_flows",
                Name = "Browse all flows",
                Description = "View 9 integration flows with custom field support matrix"
            });

            Serilog.Log.Information("Tool {Tool} completed successfully", "mcp_overview");
            return Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed", "mcp_overview");
            throw;
        }
    }
}

