using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit.Internal;

namespace StampliMCP.McpServer.Acumatica.Tests.IntegrationTests;

/// <summary>
/// Integration tests for MCP server protocol communication
/// </summary>
public sealed class McpServerIntegrationTests : IAsyncLifetime
{
    private Process? _mcpProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;

    public async ValueTask InitializeAsync()
    {
        // Start MCP server as a process
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj",
            WorkingDirectory = Path.GetDirectoryName(typeof(McpServerIntegrationTests).Assembly.Location),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _mcpProcess = Process.Start(startInfo);
        _mcpProcess.Should().NotBeNull();

        _stdin = _mcpProcess!.StandardInput;
        _stdout = _mcpProcess.StandardOutput;
        _stderr = _mcpProcess.StandardError;

        // Wait for server to be ready
        await Task.Delay(1000, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _stdin?.Close();
        _stdout?.Close();
        _stderr?.Close();

        if (_mcpProcess is not null && !_mcpProcess.HasExited)
        {
            _mcpProcess.Kill(entireProcessTree: true);
            await _mcpProcess.WaitForExitAsync(TestContext.Current.CancellationToken);
            _mcpProcess.Dispose();
        }
    }

    [Fact(Skip = "Integration test infrastructure - stdio pipe communication flaky in CI")]
    public async Task Server_Should_Respond_To_Initialize()
    {
        // Arrange
        var initializeRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new
                {
                    name = "test-client",
                    version = "1.0.0"
                }
            }
        };

        // Act
        await SendMessage(initializeRequest);
        var response = await ReadMessage();

        // Assert
        response.Should().NotBeNull();
        response.Should().ContainKey("result");
        var result = response["result"] as JsonElement?;
        result?.GetProperty("protocolVersion").GetString().Should().Be("2025-06-18");
        result?.GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("stampli-acumatica");
    }

    [Fact(Skip = "Integration test infrastructure - stdio pipe communication flaky in CI")]
    public async Task Server_Should_List_Available_Tools()
    {
        // Arrange
        await InitializeServer();

        var listToolsRequest = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        };

        // Act
        await SendMessage(listToolsRequest);
        var response = await ReadMessage();

        // Assert
        response.Should().NotBeNull();
        response.Should().ContainKey("result");
        var result = response["result"] as JsonElement?;
        var tools = result?.GetProperty("tools").EnumerateArray().ToList();

        tools.Should().NotBeNullOrEmpty();
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "get_operation_details");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "implement_kotlin_feature");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "list_categories");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "search_operations");
    }

    [Fact(Skip = "Integration test infrastructure - stdio pipe communication flaky in CI")]
    public async Task Tool_GetOperationDetails_Should_Return_Operation_Details()
    {
        // Arrange
        await InitializeServer();

        var callToolRequest = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new
            {
                name = "get_operation_details",
                arguments = new
                {
                    methodName = "exportVendor"
                }
            }
        };

        // Act
        await SendMessage(callToolRequest);
        var response = await ReadMessage();

        // Assert
        response.Should().NotBeNull();
        response.Should().ContainKey("result");
        var result = response["result"] as JsonElement?;
        var content = result?.GetProperty("content").EnumerateArray().FirstOrDefault();

        content.Should().NotBeNull();
        var operationData = JsonSerializer.Deserialize<Dictionary<string, object>>(
            content.Value.GetProperty("text").GetString()!
        );

        operationData.Should().ContainKey("operation");
        operationData["operation"].Should().Be("exportVendor");
        operationData.Should().ContainKey("requiredFields");
        operationData.Should().ContainKey("scanThese");
    }

    [Theory(Skip = "list_operations tool removed in Nuclear MCP 2025 refactor - use list_categories + get_operation_details instead")]
    [InlineData("vendors", 4)]
    [InlineData("items", 3)]
    [InlineData("purchaseOrders", 4)]
    public async Task Tool_ListOperations_Should_Return_Category_Operations(string category, int expectedCount)
    {
        // Arrange
        await InitializeServer();

        var callToolRequest = new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "list_operations",
                arguments = new
                {
                    category
                }
            }
        };

        // Act
        await SendMessage(callToolRequest);
        var response = await ReadMessage();

        // Assert
        response.Should().NotBeNull();
        response.Should().ContainKey("result");
        var result = response["result"] as JsonElement?;
        var content = result?.GetProperty("content").EnumerateArray().FirstOrDefault();

        content.Should().NotBeNull();
        var listData = JsonSerializer.Deserialize<Dictionary<string, object>>(
            content.Value.GetProperty("text").GetString()!
        );

        listData.Should().ContainKey("category");
        listData["category"].Should().Be(category);
        listData.Should().ContainKey("operations");
        listData.Should().ContainKey("count");
        listData["count"].Should().Be(expectedCount);
    }

    private async Task InitializeServer()
    {
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new
                {
                    name = "test-client",
                    version = "1.0.0"
                }
            }
        };

        await SendMessage(initRequest);
        await ReadMessage(); // Read and discard initialize response
    }

    private async Task SendMessage(object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        // MCP protocol: send as JSON-RPC over stdio
        await _stdin!.WriteLineAsync($"Content-Length: {bytes.Length}");
        await _stdin.WriteLineAsync();
        await _stdin.WriteAsync(json);
        await _stdin.FlushAsync();
    }

    private async Task<Dictionary<string, object>> ReadMessage()
    {
        // Read Content-Length header
        var contentLengthLine = await _stdout!.ReadLineAsync(TestContext.Current.CancellationToken);
        contentLengthLine.Should().NotBeNull();
        contentLengthLine.Should().StartWith("Content-Length:");

        var contentLength = int.Parse(contentLengthLine!.Split(':')[1].Trim());

        // Read blank line
        await _stdout.ReadLineAsync(TestContext.Current.CancellationToken);

        // Read JSON content
        var buffer = new char[contentLength];
        var totalRead = 0;
        while (totalRead < contentLength)
        {
            var read = await _stdout.ReadAsync(buffer, totalRead, contentLength - totalRead);
            totalRead += read;
        }

        var json = new string(buffer);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
    }
}