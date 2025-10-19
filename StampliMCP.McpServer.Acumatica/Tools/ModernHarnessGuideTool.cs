using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ModernHarnessGuideTool
{
    [McpServerTool(
        Name = "modern_harness_guide",
        Title = "Modern Test Harness & DSL Guide",
        UseStructuredContent = true
    )]
    [Description(@"
Complete guide to LiveErpTestBase, DSLs, and recording infrastructure for Kotlin tests.

This tool provides:
- LiveErpTestBase lifecycle and features
- DSL usage (Dsl.vendor, Dsl.purchaseOrder, etc.)
- Maven profiles (modern-it, modern-live, modern-live-env1)
- Recording infrastructure (ModernRecords, NDJSON events)
- ENV1 connection details
- Example test structure

Use this when:
- Writing new Kotlin tests
- Understanding test harness capabilities
- Setting up test environment
- Debugging test execution
")]
    public static async Task<ModelContextProtocol.Protocol.CallToolResult> Execute(
        KnowledgeService knowledge,
        CancellationToken ct = default)
    {
        try
        {
            Serilog.Log.Information("Tool {Tool} started", "modern_harness_guide");

            // Load from embedded knowledge
            var infrastructure = await knowledge.GetModernInfrastructureAsync(ct);

            var guide = new
            {
                summary = "Modern Kotlin test infrastructure with LiveErpTestBase harness + DSLs + ENV1",
                
                baseClass = new
                {
                    name = "LiveErpTestBase",
                    location = "kotlin-drivers-common/src/main/kotlin/com/stampli/finsys/modern/testing/LiveErpTestBase.kt",
                    extends = "Base test class for all live ERP tests",
                    lifecycle = new[] { "setup", "execute", "verify", "cleanup", "record" },
                    features = new[]
                    {
                        "Automatic cleanup on test failure or exception",
                        "Connection management with ENV1 environment",
                        "Result recording to target/modern-live/<timestamp>/",
                        "Assertion helpers: assertNoError(), assertFieldEquals()",
                        "DSL integration: driver, connectionProperties available"
                    },
                    usage = @"
class YourFeatureTest : LiveErpTestBase() {
    @Test
    @Tag(""live-acumatica"")
    fun `test feature works`() {
        // Arrange - build test data
        val testData = Dsl.vendor(""C1"") {
            vendorId(""TEST-${System.currentTimeMillis()}"")
            name(""Test Vendor"")
        }
        
        // Act - call driver operation
        val response = driver.yourOperation(createRequest(testData.toMap()))
        
        // Assert
        assertThat(response.error).isNull()
        assertThat(response.response).isNotNull()
        
        // Record for analysis
        recordExport(ExportKey.from(response))
    }
}"
                },
                
                dsls = new object[]
                {
                    new
                    {
                        name = "Dsl.vendor",
                        purpose = "Build vendor test data with Kotlin DSL",
                        example = @"
val vendor = Dsl.vendor(""C1"") {
    vendorId(""V-${timestamp}"")
    name(""Acme Corporation"")
    vendorClass(""VENDOR"")
    customField(""Department"", ""Finance"")
    address {
        line1(""123 Main St"")
        city(""San Francisco"")
        state(""CA"")
        zip(""94105"")
    }
    bank {
        accountId(""CHK-001"")
        routingNumber(""123456789"")
        bankName(""Chase Bank"")
    }
}
// Convert to Map for Java driver
val rawData = vendor.toMap()"
                    },
                    new
                    {
                        name = "Dsl.purchaseOrder",
                        purpose = "Build PO test data",
                        example = @"
val po = Dsl.purchaseOrder(""C1"") {
    poNumber(""PO-${timestamp}"")
    vendorId(""V-123"")
    date(LocalDate.now())
    line {
        itemId(""WIDGET"")
        quantity(BigDecimal(""10""))
        unitPrice(BigDecimal(""25.50""))
        description(""Test Widget"")
    }
    line {
        itemId(""GADGET"")
        quantity(BigDecimal(""5""))
        unitPrice(BigDecimal(""100.00""))
    }
}"
                    }
                },
                
                mavenProfiles = new object[]
                {
                    new
                    {
                        name = "modern-it",
                        command = "mvn test -Pmodern-it",
                        scope = "Integration tests (mock/stub, no live ERP calls)",
                        tags = "@Tag(\"integration\")"
                    },
                    new
                    {
                        name = "modern-live",
                        command = "mvn test -Pmodern-live",
                        scope = "Live ERP tests against ENV1",
                        tags = "@Tag(\"live-acumatica\"), @Tag(\"live-netsuite\"), etc.",
                        note = "Requires ENV1 connectivity"
                    },
                    new
                    {
                        name = "modern-live-env1",
                        command = "mvn test -Pmodern-live-env1",
                        scope = "Vendor probe only (always green smoke test)",
                        tags = "@Tag(\"vendor-probe\")",
                        note = "Used for CI/CD health checks"
                    }
                },
                
                recording = new
                {
                    library = "ModernRecords (kotlinx-serialization-json)",
                    manifest = "target/modern-live/<timestamp>/run.json",
                    events = "target/modern-live/<timestamp>/events.ndjson",
                    report = "target/modern-live-report.json",
                    customPath = "-Dlive.outDir=/custom/path to override",
                    usage = @"
// Recording happens automatically in LiveErpTestBase
// Manual recording:
recordExport(ExportKey.from(response))
recordImport(ImportKey.from(response))
recordEvent(""custom-event"", mapOf(""key"" to ""value""))"
                },
                
                env1 = new
                {
                    host = "http://63.32.187.185/StampliAcumaticaDB",
                    user = "admin",
                    password = "Password1",
                    subsidiary = "StampliCompany",
                    note = "Test environment - vendors/data may be deleted between test runs",
                    preconditions = "Use DSLs to create test data, don't rely on existing data"
                },
                
                exampleTest = @"
package com.stampli.finsys.modern.acumatica

import com.stampli.finsys.modern.testing.LiveErpTestBase
import com.stampli.finsys.modern.testing.Dsl
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import org.assertj.core.api.Assertions.assertThat

class ExportInvoiceTest : LiveErpTestBase() {
    
    @Test
    @Tag(""live-acumatica"")
    fun `export invoice successfully creates in Acumatica`() {
        // Arrange - Create test vendor first
        val vendor = Dsl.vendor(""C1"") {
            vendorId(""TEST-V-${System.currentTimeMillis()}"")
            name(""Test Vendor for Invoice"")
        }
        val vendorResponse = driver.exportVendor(createRequest(vendor.toMap()))
        assertThat(vendorResponse.error).isNull()
        
        // Arrange - Create invoice data
        val invoice = Dsl.invoice(""C1"") {
            vendorId(vendor.vendorId)
            invoiceNumber(""INV-${System.currentTimeMillis()}"")
            amount(BigDecimal(""1000.00""))
            line {
                description(""Service Fee"")
                amount(BigDecimal(""1000.00""))
            }
        }
        
        // Act
        val response = driver.exportInvoice(createRequest(invoice.toMap()))
        
        // Assert
        assertThat(response.error).isNull()
        assertThat(response.response).isNotNull()
        assertThat(response.response.erpInvoiceId).isNotBlank()
        
        // Record
        recordExport(ExportKey.from(response))
    }
}",
                
                troubleshooting = new string[]
                {
                    "Connection failures: Check ENV1 is accessible (ping 63.32.187.185)",
                    "Test data conflicts: Use timestamp in IDs to ensure uniqueness",
                    "Cleanup failures: LiveErpTestBase logs cleanup errors but continues",
                    "Recording not working: Check -Dlive.outDir path is writable",
                    "DSL compilation errors: Ensure kotlin-drivers-common is on classpath"
                }
            };

            var result = new ModelContextProtocol.Protocol.CallToolResult();
            result.StructuredContent = System.Text.Json.JsonSerializer.SerializeToNode(new { result = guide });
            
            // Serialize full guide as JSON for LLM consumption
            var jsonOutput = System.Text.Json.JsonSerializer.Serialize(guide, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            result.Content.Add(new ModelContextProtocol.Protocol.TextContentBlock 
            { 
                Type = "text", 
                Text = jsonOutput
            });
            
            // Resource links to related tools
            result.Content.Add(new ModelContextProtocol.Protocol.ResourceLinkBlock
            {
                Uri = "mcp://stampli-acumatica/kotlin_tdd_workflow",
                Name = "Generate TDD workflow",
                Description = "Get complete workflow for implementing a feature"
            });
            
            result.Content.Add(new ModelContextProtocol.Protocol.ResourceLinkBlock
            {
                Uri = "mcp://stampli-acumatica/get_kotlin_golden_reference",
                Name = "View Kotlin golden reference",
                Description = "See exportVendor Kotlin implementation"
            });
            
            Serilog.Log.Information("Tool {Tool} completed", "modern_harness_guide");
            return result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "modern_harness_guide", ex.Message);
            var result = new ModelContextProtocol.Protocol.CallToolResult();
            result.Content.Add(new ModelContextProtocol.Protocol.TextContentBlock
            {
                Type = "text",
                Text = $"Error: {ex.Message}"
            });
            return result;
        }
    }
}

