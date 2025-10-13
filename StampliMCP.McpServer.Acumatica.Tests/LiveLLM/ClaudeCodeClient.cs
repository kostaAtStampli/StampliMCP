using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Wrapper for Claude Code CLI to interact with MCP
/// </summary>
public sealed class ClaudeCodeClient : IDisposable
{
    private readonly string _workingDirectory;
    private readonly ConversationLogger _logger;
    private Process? _process;
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _errorOutput = new();

    public ClaudeCodeClient(string workingDirectory, ConversationLogger logger)
    {
        _workingDirectory = workingDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Start Claude Code CLI with MCP configuration
    /// </summary>
    public async Task<bool> StartAsync()
    {
        try
        {
            // Create MCP config for Claude Code
            var mcpConfig = CreateMCPConfig();
            var configPath = Path.Combine(_workingDirectory, ".claude-code-config.json");
            await File.WriteAllTextAsync(configPath, mcpConfig);

            _logger.LogFileOperation("create", configPath, mcpConfig);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Claude Code: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send a prompt to Claude CLI and wait for completion
    /// </summary>
    public async Task<string> SendPromptAsync(string prompt, TimeSpan timeout)
    {
        _logger.LogLLMTurn(prompt, "", null);

        // Use actual claude CLI command
        var startInfo = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = $"\"{prompt.Replace("\"", "\\\"")}\"",
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false // Show output for debugging
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Claude CLI");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"Claude timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Claude stderr: {error}");
        }

        _logger.LogLLMTurn(prompt, output, null);
        return output;
    }

    /// <summary>
    /// Check if files were created in the workspace
    /// </summary>
    public List<string> GetCreatedFiles()
    {
        var files = new List<string>();
        if (Directory.Exists(_workingDirectory))
        {
            files.AddRange(Directory.GetFiles(_workingDirectory, "*.kt", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(_workingDirectory, "*.java", SearchOption.AllDirectories));
        }
        return files;
    }

    private string CreateMCPConfig()
    {
        // This would configure Claude Code to use our MCP server
        var config = new
        {
            mcpServers = new
            {
                stampli_acumatica = new
                {
                    command = "dotnet",
                    args = new[]
                    {
                        "run",
                        "--project",
                        Path.GetFullPath("StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj")
                    },
                    env = new
                    {
                        MCP_DEBUG = "false"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    public void Dispose()
    {
        _process?.Kill();
        _process?.Dispose();
    }
}
