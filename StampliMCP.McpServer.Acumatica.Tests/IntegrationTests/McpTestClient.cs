using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StampliMCP.McpServer.Acumatica.Tests.IntegrationTests;

/// <summary>
/// Robust MCP test client with retry logic and proper stdio handling
/// </summary>
public sealed class McpTestClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly StreamReader _stderr;
    private readonly SemaphoreSlim _communicationLock = new(1, 1);
    private readonly StringBuilder _stderrBuffer = new();
    private readonly bool _captureStderr;
    private int _nextId = 1;
    private bool _initialized;

    public McpTestClient(bool captureStderr = false, bool debugMode = false)
    {
        _captureStderr = captureStderr;

        // Set debug mode if requested
        if (debugMode)
        {
            Environment.SetEnvironmentVariable("MCP_DEBUG", "true");
        }

        // Configure process to run MCP server
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Debug",
            WorkingDirectory = Path.GetDirectoryName(typeof(McpTestClient).Assembly.Location),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment =
            {
                ["MCP_DEBUG"] = debugMode ? "true" : "false"
            }
        };

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start MCP server process");
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
        _stderr = _process.StandardError;

        // Start capturing stderr if requested
        if (_captureStderr)
        {
            Task.Run(async () =>
            {
                while (!_process.HasExited)
                {
                    var line = await _stderr.ReadLineAsync();
                    if (line != null)
                    {
                        lock (_stderrBuffer)
                        {
                            _stderrBuffer.AppendLine(line);
                        }
                    }
                }
            });
        }

        // Wait for server to be ready
        Thread.Sleep(1000);
    }

    /// <summary>
    /// Initialize the MCP server connection
    /// </summary>
    public async Task<JsonNode> Initialize(CancellationToken ct = default)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "test-client",
                    ["version"] = "1.0.0"
                }
            }
        };

        var response = await SendRequestAsync(request, ct);
        _initialized = true;
        return response;
    }

    /// <summary>
    /// List all available tools
    /// </summary>
    public async Task<JsonArray> ListTools(CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "tools/list"
        };

        var response = await SendRequestAsync(request, ct);
        return response["result"]?["tools"]?.AsArray() ?? new JsonArray();
    }

    /// <summary>
    /// Call a specific tool with arguments
    /// </summary>
    public async Task<JsonNode> CallTool(string toolName, object arguments, CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = toolName,
                ["arguments"] = JsonSerializer.SerializeToNode(arguments)
            }
        };

        var response = await SendRequestAsync(request, ct);

        // Extract the actual tool response from the MCP wrapper
        if (response["result"]?["content"] is JsonArray content && content.Count > 0)
        {
            var firstContent = content[0];
            if (firstContent?["type"]?.GetValue<string>() == "text")
            {
                var text = firstContent["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        return JsonNode.Parse(text) ?? response;
                    }
                    catch
                    {
                        // If parsing fails, return the raw response
                        return response;
                    }
                }
            }
        }

        return response;
    }

    /// <summary>
    /// Send a raw JSON-RPC request
    /// </summary>
    public async Task<JsonNode> SendRequestAsync(JsonNode request, CancellationToken ct = default)
    {
        await _communicationLock.WaitAsync(ct);
        try
        {
            // Send request with retry logic
            await SendMessageWithRetryAsync(request, ct);

            // Read response with retry logic
            return await ReadResponseWithRetryAsync(ct);
        }
        finally
        {
            _communicationLock.Release();
        }
    }

    private async Task SendMessageWithRetryAsync(JsonNode message, CancellationToken ct, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var json = message.ToJsonString();

                // Send JSON-RPC message with proper framing
                await _stdin.WriteLineAsync(json.AsMemory(), ct);
                await _stdin.FlushAsync();
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                await Task.Delay(100 * (i + 1), ct);
            }
        }

        throw new TimeoutException($"Failed to send message after {maxRetries} retries");
    }

    private async Task<JsonNode> ReadResponseWithRetryAsync(CancellationToken ct, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Read the JSON-RPC response
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var line = await _stdout.ReadLineAsync(cts.Token);
                if (string.IsNullOrEmpty(line))
                {
                    throw new InvalidOperationException("Received empty response from server");
                }

                var response = JsonNode.Parse(line);
                if (response == null)
                {
                    throw new InvalidOperationException($"Failed to parse response: {line}");
                }

                // Check for JSON-RPC error
                if (response["error"] != null)
                {
                    var error = response["error"];
                    throw new InvalidOperationException(
                        $"JSON-RPC Error {error["code"]}: {error["message"]}");
                }

                return response;
            }
            catch (OperationCanceledException) when (i < maxRetries - 1)
            {
                await Task.Delay(100 * (i + 1), ct);
            }
        }

        throw new TimeoutException($"Failed to read response after {maxRetries} retries");
    }

    /// <summary>
    /// Get captured stderr output (if enabled)
    /// </summary>
    public string GetStderrOutput()
    {
        lock (_stderrBuffer)
        {
            return _stderrBuffer.ToString();
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }
    }

    private int GetNextId()
    {
        return Interlocked.Increment(ref _nextId);
    }

    public void Dispose()
    {
        _communicationLock?.Dispose();

        try
        {
            _stdin?.Close();
            _stdout?.Close();
            _stderr?.Close();

            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
            _process.Dispose();
        }
        catch
        {
            // Ignore errors during disposal
        }
    }
}