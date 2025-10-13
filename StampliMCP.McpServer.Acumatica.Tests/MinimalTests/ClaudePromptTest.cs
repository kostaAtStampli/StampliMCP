using System.Diagnostics;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.MinimalTests;

/// <summary>
/// Test that we can send a prompt to Claude CLI and get a response back
/// </summary>
public sealed class ClaudePromptTest
{
    [Fact]
    public async Task ClaudeCli_Should_Accept_Prompt_And_Return_Response()
    {
        // Arrange
        Console.WriteLine("=== Testing Claude CLI with actual prompt ===");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = "bash -c \"~/.local/bin/claude --print --dangerously-skip-permissions 'What is 2+2?'\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
        Console.WriteLine("Waiting for Claude to respond...");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull("Claude CLI process should start");

        // CRITICAL: Close stdin immediately so Claude knows no input is coming
        process!.StandardInput.Close();

        var timeout = TimeSpan.FromSeconds(60); // Give Claude time to respond
        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process!.Kill(true);
            Assert.Fail($"Claude CLI timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        // Log results
        Console.WriteLine($"\n=== Exit Code: {process.ExitCode} ===");
        Console.WriteLine($"\n=== STDOUT ({output.Length} chars) ===");
        Console.WriteLine(output);

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"\n=== STDERR ({error.Length} chars) ===");
            Console.WriteLine(error);
        }

        // Assert
        process.ExitCode.Should().Be(0, "Claude CLI should exit successfully");
        output.Should().NotBeNullOrEmpty("Claude should return a response");
        output.ToLower().Should().Contain("4", "Claude should answer that 2+2=4");

        Console.WriteLine("\n✓ SUCCESS - Claude CLI accepted prompt and returned response!");
    }

    [Fact]
    public async Task ClaudeCli_Should_List_MCP_Tools()
    {
        // Arrange
        Console.WriteLine("=== Testing Claude CLI with MCP tool query ===");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = "bash -c \"~/.local/bin/claude --print --dangerously-skip-permissions 'List all available MCP tools from the stampli-acumatica server. Just list the tool names.'\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
        Console.WriteLine("Waiting for Claude to query MCP server...");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull("Claude CLI process should start");

        // CRITICAL: Close stdin immediately so Claude knows no input is coming
        process!.StandardInput.Close();

        var timeout = TimeSpan.FromSeconds(60);
        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process!.Kill(true);
            Assert.Fail($"Claude CLI timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        // Log results
        Console.WriteLine($"\n=== Exit Code: {process.ExitCode} ===");
        Console.WriteLine($"\n=== STDOUT ({output.Length} chars) ===");
        Console.WriteLine(output);

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"\n=== STDERR ({error.Length} chars) ===");
            Console.WriteLine(error);
        }

        // Assert
        process.ExitCode.Should().Be(0, "Claude CLI should exit successfully");
        output.Should().NotBeNullOrEmpty("Claude should return MCP tool information");

        // Check if our MCP tools are mentioned
        var outputLower = output.ToLower();
        var hasMcpTools = outputLower.Contains("kotlin") ||
                         outputLower.Contains("workflow") ||
                         outputLower.Contains("health") ||
                         outputLower.Contains("stampli") ||
                         outputLower.Contains("acumatica");

        hasMcpTools.Should().BeTrue("Response should mention MCP tools from our server");

        Console.WriteLine("\n✓ SUCCESS - Claude CLI can query our MCP server!");
    }
}
