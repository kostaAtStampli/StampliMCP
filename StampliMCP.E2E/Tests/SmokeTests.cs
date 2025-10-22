using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using StampliMCP.E2E.Infrastructure;

namespace StampliMCP.E2E.Tests;

[Collection("MCP-Server")]
public class SmokeTests
{
    private readonly McpServerFixture _fx;
    public SmokeTests(McpServerFixture fx) => _fx = fx;

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var client = _fx.Client ?? throw new InvalidOperationException("Client not initialized");
        var res = await client.CallToolAsync("erp__health_check", new Dictionary<string, object?>());

        // Extract the text content from MCP response
        Assert.NotNull(res.Content);
        Assert.NotEmpty(res.Content);
        var textContent = res.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(textContent);
        var responseText = textContent.Text;

        Assert.Contains("\"status\":", responseText);
        Assert.Contains("\"ok\"", responseText);
        Assert.Contains("registeredErps", responseText);
    }

    [Fact]
    public async Task ListErps_ReturnsModules()
    {
        var client = _fx.Client ?? throw new InvalidOperationException("Client not initialized");
        var res = await client.CallToolAsync("erp__list_erps", new Dictionary<string, object?>());

        // Extract the text content from MCP response
        var textContent = res.Content.FirstOrDefault(c => c.Type == "text") as TextContentBlock;
        Assert.NotNull(textContent);
        var responseText = textContent.Text;

        Assert.Contains("acumatica", responseText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("intacct", responseText, StringComparison.OrdinalIgnoreCase);
    }
}
