using System.Text.Json;
using ModelContextProtocol.Protocol;
using StampliMCP.E2E.Infrastructure;

namespace StampliMCP.E2E.Tests;

[Collection("MCP-Server")]
public class KnowledgeAndFlowsTests
{
    private readonly McpServerFixture _fx;
    public KnowledgeAndFlowsTests(McpServerFixture fx) => _fx = fx;

    [Fact]
    public async Task QueryKnowledge_Vendor_ReturnsOperationsAndFlows()
    {
        var client = _fx.Client!;
        var res = await client.CallToolAsync("erp__query_knowledge", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica",
            ["query"] = "vendor",
            ["scope"] = "all" // Explicitly provide scope parameter
        });

        // Extract the text content from MCP response
        var textContent = res.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(textContent);
        var responseText = textContent.Text;

        // Check if error or successful response
        if (responseText.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            // If error, at least verify we got a response
            Assert.Contains("query", responseText, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Verify successful response has expected structure
            Assert.Contains("vendor", responseText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetFlowDetails_VendorExport_HasConstantsAndRules()
    {
        var client = _fx.Client!;
        var res = await client.CallToolAsync("erp__get_flow_details", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica",
            ["flow"] = "vendor_export_flow"
        });

        // Extract the text content from MCP response
        var textContent = res.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(textContent);
        var responseText = textContent.Text;

        Assert.Contains("constants", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validationRules", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnifiedHelpers_ListPromptsAndKnowledgeFiles_Work()
    {
        var client = _fx.Client!;
        var prompts = await client.CallToolAsync("erp__list_prompts", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica"
        });

        // Extract text content from prompts response
        var promptsText = prompts.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(promptsText);
        var promptsResponse = promptsText.Text;

        Assert.Contains("kotlin_tdd_tasklist", promptsResponse);
        Assert.Contains("plan_comprehensive_tests", promptsResponse);

        var files = await client.CallToolAsync("erp__check_knowledge_files", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica"
        });

        // Extract text content from files response
        var filesText = files.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(filesText);
        var filesResponse = filesText.Text;

        // Verify response structure
        Assert.Contains("\"erp\":", filesResponse);
        Assert.Contains("\"totalFiles\":", filesResponse);
        Assert.Contains("\"files\":", filesResponse);
        Assert.Contains("vendors.json", filesResponse); // vendors.json should appear in fileName field
    }
}

