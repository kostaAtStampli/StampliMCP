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
    private string? _configPath; // Store config path for later use
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
            _configPath = configPath; // Store for later use
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

        if (_configPath == null)
            throw new InvalidOperationException("Must call StartAsync first");

        // Use actual claude CLI command via WSL (since claude is installed in WSL)
        // Convert Windows path to WSL path for working directory
        var wslWorkingDir = _workingDirectory.Replace("C:", "/mnt/c").Replace("\\", "/");
        var wslConfigPath = _configPath.Replace("C:", "/mnt/c").Replace("\\", "/");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"cd '{wslWorkingDir}' && claude --mcp-config '{wslConfigPath}' --print --permission-mode bypassPermissions --debug '{prompt.Replace("'", "'\\''")}' \"",
            WorkingDirectory = _workingDirectory,
            RedirectStandardInput = true,   // For auto-approval
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

        // Auto-answer permission prompt with "2" (Yes, I accept)
        await process.StandardInput.WriteLineAsync("2");
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

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
            Console.WriteLine($"Claude debug output:\n{error}");

            // Track MCP tool calls from debug output
            if (error.Contains("kotlin_tdd_workflow") || error.Contains("mcp"))
            {
                _logger.LogToolCall("kotlin_tdd_workflow", "detected in debug output");
            }
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
        // Use absolute path to avoid relative path issues
        var repoRoot = @"C:\Users\Kosta\source\repos\StampliMCP";
        var projectPath = Path.Combine(repoRoot, "StampliMCP.McpServer.Acumatica", "StampliMCP.McpServer.Acumatica.csproj");

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
                        projectPath
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
