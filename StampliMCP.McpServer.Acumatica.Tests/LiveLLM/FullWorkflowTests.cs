using Xunit;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Integration tests using real LLM (Claude Code or Continue)
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

    [Fact(Skip = "Requires Claude Code CLI and API key")]
    public async Task ClaudeCode_Should_Implement_ExportVendor_Via_TDD()
    {
        // Arrange
        var sandbox = _sandboxManager.CreateSandbox("claude_export_vendor");
        var logger = new ConversationLogger("ClaudeCode_ExportVendor");

        try
        {
            using var client = new ClaudeCodeClient(sandbox, logger);

            // Start Claude Code with MCP config
            var started = await client.StartAsync();
            started.Should().BeTrue("Claude Code should start successfully");

            // Act - Give it the task
            var prompt = @"
You have access to the kotlin_tdd_workflow MCP tool.

Task: Implement the exportVendor operation in Kotlin following strict TDD methodology.

Steps:
1. Use kotlin_tdd_workflow with command='start' and context='export vendor to Acumatica'
2. Read the returned knowledge (test templates, implementation templates, validation rules)
3. Write the test file: src/test/kotlin/KotlinAcumaticaDriverTest.kt
4. Run tests and verify they fail (RED phase)
5. Report back with command='continue' and context='tests are failing'
6. Implement the code in: src/main/kotlin/KotlinAcumaticaDriver.kt
7. Run tests until they pass (GREEN phase)
8. Report back with command='continue' and context='all tests passing'
9. Complete the workflow

Write all files to the current working directory.
";

            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(5));

            // Assert
            response.Should().NotBeNullOrEmpty();

            var files = client.GetCreatedFiles();
            files.Should().Contain(f => f.Contains("KotlinAcumaticaDriverTest.kt"));
            files.Should().Contain(f => f.Contains("KotlinAcumaticaDriver.kt"));

            // Log success
            logger.SaveConversation(success: true);
        }
        catch (Exception ex)
        {
            // Log failure
            logger.SaveConversation(success: false, errorMessage: ex.Message);
            throw;
        }
        finally
        {
            _sandboxManager.CleanupSandbox(sandbox);
        }
    }

    [Fact(Skip = "Manual test for MCP tool verification")]
    public async Task Manual_MCP_Tool_Verification()
    {
        // This is a simpler test that just verifies MCP tools are accessible
        // Can be run manually when Claude Code / Continue is configured

        var sandbox = _sandboxManager.CreateSandbox("manual_mcp_test");
        var logger = new ConversationLogger("Manual_MCP_Verification");

        try
        {
            using var client = new ClaudeCodeClient(sandbox, logger);
            await client.StartAsync();

            var prompt = "List all available MCP tools and tell me what you see.";
            var response = await client.SendPromptAsync(prompt, TimeSpan.FromMinutes(1));

            Console.WriteLine("=== MCP Tools Available ===");
            Console.WriteLine(response);

            logger.SaveConversation(success: true);
        }
        catch (Exception ex)
        {
            logger.SaveConversation(success: false, errorMessage: ex.Message);
            Console.WriteLine($"Test info: {ex.Message}");
        }
        finally
        {
            _sandboxManager.CleanupSandbox(sandbox);
        }
    }

    public void Dispose()
    {
        _sandboxManager.Dispose();
    }
}
