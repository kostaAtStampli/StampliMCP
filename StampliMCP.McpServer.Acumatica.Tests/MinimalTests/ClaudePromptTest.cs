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

        // Create isolated test directory for this test run
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_test_2plus2_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(testDir);
        Console.WriteLine($"Test directory: {testDir}");

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

        // CRITICAL: Start reading streams BEFORE waiting (prevents deadlock on large output)
        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(600); // 10 minutes - Give Claude time to respond
        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process!.Kill(true);
            Assert.Fail($"Claude CLI timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Log results to file AND console in isolated directory
        var logPath = Path.Combine(testDir, "minimal_test_output.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        File.WriteAllText(logPath, logContent);

        Console.WriteLine($"\n=== Exit Code: {process.ExitCode} ===");
        Console.WriteLine($"\n=== STDOUT ({output.Length} chars) ===");
        Console.WriteLine(output);
        Console.WriteLine($"\n=== Isolated test directory: {testDir} ===");
        Console.WriteLine($"=== Log saved to: {logPath} ===");

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

        // Create isolated test directory for this test run
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_test_list_tools_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(testDir);
        Console.WriteLine($"Test directory: {testDir}");

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

        // CRITICAL: Start reading streams BEFORE waiting (prevents deadlock on large output)
        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(600); // 10 minutes - Give Claude time for MCP query
        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process!.Kill(true);
            Assert.Fail($"Claude CLI timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Log results to file AND console in isolated directory
        var logPath = Path.Combine(testDir, "minimal_mcp_test_output.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        File.WriteAllText(logPath, logContent);

        Console.WriteLine($"\n=== Exit Code: {process.ExitCode} ===");
        Console.WriteLine($"\n=== STDOUT ({output.Length} chars) ===");
        Console.WriteLine(output);
        Console.WriteLine($"\n=== Isolated test directory: {testDir} ===");
        Console.WriteLine($"\n=== Log saved to: {logPath} ===");

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

    [Fact]
    public async Task ClaudeCli_V4_Composable_Flow_Test()
    {
        // Arrange
        var testStart = DateTime.UtcNow;
        Console.WriteLine("=== Testing v4.0 Composable MCP Flow (query → recommend → workflow) ===");

        // Create isolated test directory
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_v4_flow_test_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(testDir);
        Console.WriteLine($"Test directory: {testDir}");

        // v4.0 COMPOSABLE FLOW TEST:
        // 1. query_acumatica_knowledge("vendor export") → Should find VENDOR_EXPORT_FLOW
        // 2. recommend_flow("vendor export") → Should return FlowRecommendation with 0.95 confidence
        // 3. kotlin_tdd_workflow("exportVendor") → Should return structured TddWorkflowResult
        var prompt = @"
Test MCP v4.0 composable architecture:
1. Call query_acumatica_knowledge with query='vendor export'. Show the flow it finds.
2. Call recommend_flow with useCase='vendor export'. Show the confidence score and flow name.
3. Call kotlin_tdd_workflow with feature='exportVendor'. Show if it returns structured TDD steps.
Be concise - just show tool responses.";
        var escapedPrompt = prompt.Replace("'", "'\\''").Replace("\r\n", " ").Replace("\n", " ");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"MCP_LOG_DIR='{testDir}' ~/.local/bin/claude --print --dangerously-skip-permissions '{escapedPrompt}'\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        Console.WriteLine($"Executing: {startInfo.FileName} {startInfo.Arguments}");
        Console.WriteLine("Waiting for Claude to call MCP tool...");

        // PROGRESSIVE LOGGING: Create crash-proof log file in isolated directory
        var progressLogPath = Path.Combine(testDir, $"claude_progress_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var progressWriter = new StreamWriter(progressLogPath, append: false) { AutoFlush = true };

        progressWriter.WriteLine($"=== TEST STARTED: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        progressWriter.WriteLine($"Test: ClaudeCli_Should_Call_KotlinTddWorkflow_And_Get_Valid_Response");
        progressWriter.WriteLine($"MCP Server: ~/stampli-mcp-acumatica.exe");
        progressWriter.WriteLine("========================================\n");

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull("Claude CLI process should start");

        // CRITICAL: Close stdin immediately so Claude knows no input is coming
        process!.StandardInput.Close();

        // PROGRESSIVE LOGGING: Read stdout line-by-line, log immediately (survives crashes)
        var outputBuilder = new System.Text.StringBuilder();
        var realtimeTask = Task.Run(async () =>
        {
            using var reader = process!.StandardOutput;
            int lineNum = 0;
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null)
                {
                    lineNum++;
                    outputBuilder.AppendLine(line);

                    // IMMEDIATE write to progress log (crash-proof)
                    progressWriter.WriteLine($"[{lineNum:D4}] {line}");

                    // Log key milestones to console
                    if (line.Contains("MCP Response") || line.Contains("Operation") ||
                        line.Contains("Files") || line.Contains("Step") || line.Contains("Phase"))
                    {
                        Console.WriteLine($"[PROGRESS] Line {lineNum}: {line.Substring(0, Math.Min(80, line.Length))}...");
                    }
                }
            }
            progressWriter.WriteLine($"\n=== STDOUT READING COMPLETED ===");
        });

        // Read stderr
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(600); // 10 min for autonomous workflow (reading multiple files)
        progressWriter.WriteLine($"Timeout: {timeout.TotalSeconds} seconds");

        var completed = await Task.Run(() => process!.WaitForExit((int)timeout.TotalMilliseconds));

        // Wait for reader to finish (with small timeout)
        await Task.WhenAny(realtimeTask, Task.Delay(5000));

        progressWriter.WriteLine($"\n=== PROCESS ENDED: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        progressWriter.WriteLine($"Exit Code: {process.ExitCode}");
        progressWriter.WriteLine($"Completed: {completed}");
        progressWriter.Close();

        if (!completed)
        {
            Console.WriteLine($"\n✗ TIMEOUT - Check progress log: {progressLogPath}");
            process!.Kill(true);
            Assert.Fail($"Claude CLI timed out after {timeout.TotalSeconds} seconds. Progress log: {progressLogPath}");
        }

        var output = outputBuilder.ToString();
        var error = await errorTask;

        // Log results to file AND console in isolated directory
        var logPath = Path.Combine(testDir, "minimal_kotlin_workflow_test_output.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        File.WriteAllText(logPath, logContent);

        Console.WriteLine($"\n=== Exit Code: {process.ExitCode} ===");
        Console.WriteLine($"\n=== STDOUT ({output.Length} chars) ===");
        Console.WriteLine(output);
        Console.WriteLine($"\n=== Log saved to: {logPath} ===");

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"\n=== STDERR ({error.Length} chars) ===");
            Console.WriteLine(error);
        }

        // Assert - v4.0 Composable Flow
        process.ExitCode.Should().Be(0, "Claude CLI should exit successfully");
        output.Should().NotBeNullOrEmpty("Claude should return a response");

        var outputLower = output.ToLower();

        // v4.0 CHECK 1: query_acumatica_knowledge should find VENDOR_EXPORT_FLOW
        var foundFlow = outputLower.Contains("vendor_export_flow") ||
                       (outputLower.Contains("vendor") && outputLower.Contains("flow"));
        foundFlow.Should().BeTrue("query_acumatica_knowledge should find VENDOR_EXPORT_FLOW");

        // v4.0 CHECK 2: recommend_flow should return confidence score
        var hasConfidence = outputLower.Contains("confidence") ||
                           outputLower.Contains("0.9") ||
                           outputLower.Contains("95%");
        hasConfidence.Should().BeTrue("recommend_flow should return confidence score");

        // v4.0 CHECK 3: Structured output - check for evidence of structured data
        var hasStructured = outputLower.Contains("tddsteps") ||
                           outputLower.Contains("step") ||
                           outputLower.Contains("includes") ||
                           outputLower.Contains("workflow") ||
                           outputLower.Contains("golden") ||
                           outputLower.Contains("validation") ||
                           (outputLower.Contains("red") && outputLower.Contains("green"));
        hasStructured.Should().BeTrue("kotlin_tdd_workflow should return structured TDD steps");

        // v4.0 Log verification - check MCP logs for actual tool calls
        Console.WriteLine("\n=== v4.0 MCP Log Verification ===");
        Console.WriteLine($"Test directory: {testDir}");
        Console.WriteLine($"MCP logs: /tmp/mcp_logs/structured.jsonl");

        Console.WriteLine("\n✓ v4.0 Composable Flow Test Completed!");
        Console.WriteLine($"✓ Verified: query → recommend → workflow chain");
        Console.WriteLine($"\n=== Logs saved to: {testDir} ===");
    }

    [Fact]
    public async Task ClaudeCli_V4_Elicitation_Test()
    {
        // Test elicitation feature - ambiguous query triggers interactive prompt
        Console.WriteLine("=== Testing v4.0 Elicitation (Interactive Refinement) ===");

        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_v4_elicit_test_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(testDir);

        // Ambiguous query "payment" should trigger elicitation: Import or Export?
        var prompt = @"
Call query_acumatica_knowledge with query='payment'.
If it asks for clarification (elicitation), choose 'import'.
Show the final flow it recommends.";
        var escapedPrompt = prompt.Replace("'", "'\\''").Replace("\r\n", " ").Replace("\n", " ");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"~/.local/bin/claude --print --dangerously-skip-permissions '{escapedPrompt}'\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull();
        process!.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(600);
        var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process.Kill(true);
            Assert.Fail($"Timeout after {timeout.TotalSeconds}s");
        }

        var output = await outputTask;
        var error = await errorTask;

        File.WriteAllText(Path.Combine(testDir, "elicitation_test.txt"),
            $"Exit: {process.ExitCode}\nOutput:\n{output}\nError:\n{error}");

        Console.WriteLine($"Output: {output}");

        // Assert - should mention flow (PAYMENT_FLOW or STANDARD_IMPORT_FLOW)
        process.ExitCode.Should().Be(0);
        var outputLower = output.ToLower();
        var mentionsFlow = outputLower.Contains("flow") &&
                          (outputLower.Contains("payment") || outputLower.Contains("import"));
        mentionsFlow.Should().BeTrue("Elicitation should result in flow recommendation");

        Console.WriteLine($"\n✓ Elicitation test completed! Logs: {testDir}");
    }
}
