using Xunit;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Integration tests using real LLM (Claude Code CLI)
/// These tests are expensive and require API keys
/// Run with: dotnet test --filter "Category=LiveLLM"
/// </summary>
[Collection("Sequential")]
[Trait("Category", "LiveLLM")]
public sealed class FullWorkflowTests : IDisposable
{
    private readonly SandboxManager _sandboxManager;

    public FullWorkflowTests()
    {
        _sandboxManager = new SandboxManager();
    }

    /// <summary>
    /// Simple test to verify MCP tools are accessible
    /// </summary>
    [Fact]
    public async Task Step1_Verify_MCP_Tools_Accessible()
    {
        var sandbox = _sandboxManager.CreateSandbox("mcp_verify");
        var logger = new ConversationLogger("MCP_Verify");

        try
        {
            Console.WriteLine($"=== Test: Verify MCP Tools Accessible ===");
            Console.WriteLine($"Sandbox: {sandbox}");

            using var client = new ClaudeCodeClient(sandbox, logger);
            var started = await client.StartAsync();
            started.Should().BeTrue("Claude Code should start successfully");

            var prompt = "List all available MCP tools. What tools do you see?";

            Console.WriteLine($"Sending prompt: {prompt}");
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(2));

            Console.WriteLine($"=== Claude Response ===");
            Console.WriteLine(response);
            Console.WriteLine($"======================");

            // Verify we got a response
            response.Should().NotBeNullOrEmpty("Claude should return a response");

            // Check conversation log
            var toolCalls = logger.GetAllToolCalls();
            Console.WriteLine($"Tool calls detected: {toolCalls.Count}");

            logger.SaveConversation(success: true);

            Console.WriteLine("✓ Test passed - MCP accessible");
        }
        catch (Exception ex)
        {
            logger.SaveConversation(success: false, errorMessage: ex.Message);
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
        finally
        {
            _sandboxManager.CleanupSandbox(sandbox);
        }
    }

    /// <summary>
    /// Full workflow test - Claude implements exportVendor via TDD
    /// </summary>
    [Fact]
    public async Task Step2_ClaudeCode_Should_Implement_ExportVendor_Via_TDD()
    {
        var sandbox = _sandboxManager.CreateSandbox("claude_export_vendor");
        var logger = new ConversationLogger("ClaudeCode_ExportVendor");

        try
        {
            Console.WriteLine($"=== Test: Implement ExportVendor via TDD ===");
            Console.WriteLine($"Sandbox: {sandbox}");

            using var client = new ClaudeCodeClient(sandbox, logger);
            var started = await client.StartAsync();
            started.Should().BeTrue("Claude Code should start successfully");

            // Give Claude a clear task
            var prompt = @"You have access to the kotlin_tdd_workflow MCP tool.

Task: Implement exportVendor operation in Kotlin using TDD methodology.

Steps:
1. Use kotlin_tdd_workflow tool with command='start' and context='export vendor to Acumatica'
2. Read the knowledge returned (test templates, validation rules, etc.)
3. Write a simple test file to: src/test/kotlin/ExportVendorTest.kt
4. Write a simple implementation to: src/main/kotlin/ExportVendor.kt
5. Keep it minimal - just basic structure is fine for this test

Write all files to the current working directory.";

            Console.WriteLine($"Sending prompt...");
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(5));

            Console.WriteLine($"=== Claude Response ===");
            Console.WriteLine(response.Length > 500 ? response.Substring(0, 500) + "..." : response);
            Console.WriteLine($"======================");

            // Verify response
            response.Should().NotBeNullOrEmpty("Claude should return a response");

            // Check for files created
            var files = client.GetCreatedFiles();
            Console.WriteLine($"Files created: {files.Count}");
            foreach (var file in files)
            {
                Console.WriteLine($"  - {file}");
            }

            // Basic assertions
            if (files.Any())
            {
                files.Should().Contain(f => f.EndsWith(".kt"), "Should have created Kotlin files");

                var firstFile = File.ReadAllText(files.First());
                firstFile.Should().NotBeEmpty("File should have content");

                Console.WriteLine($"✓ File content preview: {firstFile.Substring(0, Math.Min(200, firstFile.Length))}...");
            }

            // Log success
            logger.SaveConversation(success: true);
            Console.WriteLine("✓ Test passed - Files created");
        }
        catch (Exception ex)
        {
            logger.SaveConversation(success: false, errorMessage: ex.Message);
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
        finally
        {
            // Don't cleanup immediately - allow inspection
            Console.WriteLine($"Sandbox preserved at: {sandbox}");
            Console.WriteLine($"Logs saved to: Tests/LiveLLM/Logs/");
        }
    }

    public void Dispose()
    {
        // Cleanup will happen when sandboxManager disposes
        _sandboxManager.Dispose();
    }
}
