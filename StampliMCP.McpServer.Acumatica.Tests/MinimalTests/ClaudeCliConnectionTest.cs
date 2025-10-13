using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.MinimalTests;

/// <summary>
/// Minimal test to verify Claude CLI can auto-communicate with our MCP server on WSL
/// This MUST pass before any other tests
/// </summary>
public sealed class ClaudeCliConnectionTest
{

    [Fact]
    public async Task ClaudeCli_Should_Return_Version()
    {
        // Arrange
        Console.WriteLine("=== Starting Claude CLI Version Test ===");

        // Since tests run on Windows, we need to call WSL to execute claude
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = "~/.local/bin/claude --version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull("Claude CLI process should start");

        var timeout = TimeSpan.FromSeconds(5);
        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process!.Kill(true);
            Assert.Fail("Claude CLI --version timed out");
        }

        var output = await process!.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        Console.WriteLine($"Exit Code: {process.ExitCode}");
        Console.WriteLine($"STDOUT: {output}");
        Console.WriteLine($"STDERR: {error}");

        // Assert
        process.ExitCode.Should().Be(0, "Claude CLI --version should succeed");
        output.Should().NotBeNullOrEmpty("Should return version info");

        Console.WriteLine("✓ SUCCESS - Claude CLI is accessible via WSL");
    }

    [Fact]
    public async Task ClaudeCli_Should_Be_Installed_And_Accessible()
    {
        // Arrange
        Console.WriteLine("=== Verifying Claude CLI Installation ===");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = "which claude",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(startInfo);
        process.Should().NotBeNull();

        await process!.WaitForExitAsync();
        var output = await process.StandardOutput.ReadToEndAsync();

        Console.WriteLine($"Claude CLI location: {output.Trim()}");

        // Assert
        process.ExitCode.Should().Be(0, "which claude should find the executable");
        output.Should().NotBeNullOrEmpty("Claude CLI should be in PATH");

        Console.WriteLine("✓ Claude CLI is installed and accessible");
    }
}
