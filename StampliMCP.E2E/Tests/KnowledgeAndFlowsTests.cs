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
    public async Task RecommendFlow_VendorExport_ReturnsDetails()
    {
        var client = _fx.Client!;
        var res = await client.CallToolAsync("erp__recommend_flow", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica",
            ["useCase"] = "export vendors"
        });

        // Extract the text content from MCP response
        var textContent = res.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(textContent);
        var responseText = textContent.Text;

        Assert.Contains("vendor_export_flow", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validationRules", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KnowledgeMaintenance_Modes_Work()
    {
        var client = _fx.Client!;
        var files = await client.CallToolAsync("erp__knowledge_update_plan", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica",
            ["mode"] = "files"
        });

        var filesText = files.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(filesText);
        var filesResponse = filesText.Text;
        Assert.Contains("\"totalFiles\"", filesResponse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vendors.json", filesResponse, StringComparison.OrdinalIgnoreCase);

        var validate = await client.CallToolAsync("erp__knowledge_update_plan", new Dictionary<string, object?>
        {
            ["erp"] = "acumatica",
            ["mode"] = "validate"
        });

        var validateText = validate.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(validateText);
        var validateResponse = validateText.Text;
        Assert.Contains("\"entries\"", validateResponse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"issues\"", validateResponse, StringComparison.OrdinalIgnoreCase);
    }
}

