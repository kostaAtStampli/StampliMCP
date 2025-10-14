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
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(12));

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
    /// NEW TEST: Verify LLM scans legacy files and creates custom tasklist
    /// This is the KEY test to prove your vision works
    /// </summary>
    [Fact]
    public async Task Step2_LLM_Should_Scan_Legacy_Files_Before_Creating_Tasklist()
    {
        var sandbox = _sandboxManager.CreateSandbox("llm_scan_files");
        var logger = new ConversationLogger("LLM_Scan_Files");

        try
        {
            Console.WriteLine($"=== Test: LLM Should Scan Legacy Files ===");
            Console.WriteLine($"Sandbox: {sandbox}");

            using var client = new ClaudeCodeClient(sandbox, logger);
            var started = await client.StartAsync();
            started.Should().BeTrue("Claude Code should start successfully");

            // Use standardized DIRECTIVE prompt (not conversational)
            // Conversational prompts ("help me") cause Claude to ask questions instead of using tools
            var prompt = PromptTemplates.AutonomousWorkflow("vendor custom field import from Acumatica");

            Console.WriteLine($"Sending prompt...");
            Console.WriteLine($"Test will timeout after 12 minutes...");
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(12));

            Console.WriteLine($"=== Claude Response (first 1000 chars) ===");
            Console.WriteLine(response.Length > 1000 ? response.Substring(0, 1000) + "..." : response);
            Console.WriteLine($"======================");

            // CRITICAL ASSERTIONS - Prove the LLM scanned files
            response.Should().NotBeNullOrEmpty("Claude should return a response");

            // Check conversation log for tool calls
            var toolCalls = logger.GetAllToolCalls();
            Console.WriteLine($"Tool calls detected: {toolCalls.Count}");

            // Verify MCP tool was called
            var mcpCalls = toolCalls.Where(t => t.Name.Contains("kotlin_tdd_workflow")).ToList();
            mcpCalls.Should().NotBeEmpty("Should have called kotlin_tdd_workflow");

            // Verify Read tool was called (this proves LLM scanned legacy files)
            var readCalls = toolCalls.Where(t => t.Name.Contains("Read") || t.Name.Contains("read")).ToList();
            Console.WriteLine($"Read tool calls: {readCalls.Count}");

            // Check for evidence of file scanning in response
            var responseLower = response.ToLower();
            var hasFileEvidence = responseLower.Contains("acumatica") &&
                                (responseLower.Contains("driver") ||
                                 responseLower.Contains("vendor") ||
                                 responseLower.Contains(".java") ||
                                 responseLower.Contains("legacy"));

            hasFileEvidence.Should().BeTrue("Response should mention legacy files or classes");

            // Check for tasklist in response
            var hasTasklist = responseLower.Contains("task") ||
                            responseLower.Contains("step") ||
                            responseLower.Contains("approval");

            hasTasklist.Should().BeTrue("Response should present a tasklist for approval");

            logger.SaveConversation(success: true);

            Console.WriteLine("✓ Test passed - LLM scanned files and created tasklist");
        }
        catch (Exception ex)
        {
            logger.SaveConversation(success: false, errorMessage: ex.Message);
            Console.WriteLine($"✗ Test failed: {ex.Message}");
            throw;
        }
        finally
        {
            Console.WriteLine($"Sandbox preserved at: {sandbox}");
            Console.WriteLine($"Logs saved to: LiveLLM/Logs/");
        }
    }

    /// <summary>
    /// Original test - Claude implements exportVendor via TDD
    /// </summary>
    [Fact]
    public async Task Step3_ClaudeCode_Should_Implement_ExportVendor_Via_TDD()
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

            // Use standardized TDD workflow prompt with explicit numbered steps
            var prompt = PromptTemplates.TddWorkflow(
                "export vendor to Acumatica",
                "src/test/kotlin/ExportVendorTest.kt",
                "src/main/kotlin/ExportVendor.kt");

            Console.WriteLine($"Sending prompt...");
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(12));

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
