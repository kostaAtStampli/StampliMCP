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

        // Escape the prompt properly for bash - replace newlines with spaces and escape single quotes
        var escapedPrompt = prompt.Replace("\r\n", " ").Replace("\n", " ").Replace("'", "'\\''");

        // Match minimal test exactly - no --debug (conflicts with --print)
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"~/.local/bin/claude --print --dangerously-skip-permissions '{escapedPrompt}'\"",
            WorkingDirectory = _workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false // Show output for debugging
        };

        Console.WriteLine($"=== Executing Claude CLI ===");
        Console.WriteLine($"Command: {startInfo.FileName} {startInfo.Arguments}");
        Console.WriteLine($"Working dir: {startInfo.WorkingDirectory}");
        Console.WriteLine($"===========================");

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Claude CLI");
        }

        // CRITICAL: Close stdin immediately (like minimal tests) - no need to write "2" with --dangerously-skip-permissions
        process.StandardInput.Close();

        // CRITICAL: Start reading streams BEFORE waiting (prevents deadlock on large output)
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for process with timeout
        var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"Claude timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Log to file for debugging
        var logPath = Path.Combine(Path.GetTempPath(), $"livellm_test_output_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
        var logContent = $"=== Command ===\n{startInfo.FileName} {startInfo.Arguments}\n\n" +
                        $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        File.WriteAllText(logPath, logContent);

        Console.WriteLine($"=== Exit code: {process.ExitCode} ===");
        Console.WriteLine($"=== Log saved to: {logPath} ===");
        Console.WriteLine($"=== STDOUT ({output.Length} chars) ===");
        if (!string.IsNullOrEmpty(output))
            Console.WriteLine(output);
        else
            Console.WriteLine("(empty)");

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"=== STDERR ({error.Length} chars) ===");
            Console.WriteLine(error);

            // Track MCP tool calls from debug output
            if (error.Contains("kotlin_tdd_workflow") || error.Contains("mcp"))
            {
                _logger.LogToolCall("kotlin_tdd_workflow", "detected in debug output");

                // NEW: Capture complete MCP response for analysis
                // Look for JSON blocks in debug output that contain CRITICAL_STOP or WARNING
                if (error.Contains("CRITICAL_STOP") || error.Contains("WARNING") || error.Contains("MANDATORY"))
                {
                    _logger.LogMCPResponse("kotlin_tdd_workflow", "MCP response detected in debug output - see stderr for full content");
                }
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
        // CRITICAL: Use WSL paths because Claude CLI runs in WSL bash context
        var repoRoot = "/mnt/c/Users/Kosta/source/repos/StampliMCP";
        var projectPath = $"{repoRoot}/StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj";

        var config = new
        {
            mcpServers = new
            {
                stampli_acumatica = new
                {
                    command = "/mnt/c/Program Files/dotnet/dotnet.exe",
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
