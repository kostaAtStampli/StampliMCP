using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class AddKnowledgeFromPrTool
{
    [McpServerTool(
        Name = "add_knowledge_from_pr",
        Title = "Add Knowledge From PR",
        UseStructuredContent = true
    )]
    [Description(@"
Spawns Claude CLI to intelligently add knowledge from PR learnings.

This tool:
1. Checks for duplicates via query_acumatica_knowledge
2. Makes triage decision (ADD/SKIP/DUPLICATE/BACKLOG)
3. If ADD: Updates operations/*.json and categories.json
4. Rebuilds MCP server
5. Returns structured result

IMPORTANT: Learning MUST include code location (file path + line numbers) to be added.

Examples:
- ""void-and-reissue requires ApprovedForPayment=false. See PaymentHandler.java:234-289""
- ""Custom field export for PO uses Details.Attribute* DAC. See POSerializer.java:145-160""

Usage: Callable from any Claude Code session to update MCP knowledge base.
")]
    public static async Task<CallToolResult> Execute(
        [Description("PR number (e.g., '#456', '456', or 'PR-456')")]
        string prNumber,

        [Description("What you learned from this PR - MUST include code location (file path + line numbers)")]
        string learnings
    )
    {
        Serilog.Log.Information("Tool {Tool} started: Adding knowledge from PR {PR}",
            "add_knowledge_from_pr", prNumber);

        try
        {
            // Create temp directory for prompt file
            var testDir = Path.Combine(Path.GetTempPath(), $"mcp_add_knowledge_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(testDir);

            Serilog.Log.Information("Created temp directory: {TestDir}", testDir);

            // Build prompt for Claude CLI with TWO-SCAN ENFORCEMENT
            var prompt = $@"
You are updating the Stampli MCP Acumatica knowledge base from PR {prNumber}.

Learning: {learnings}

WORKFLOW: 7-Step Knowledge Addition with Two-Scan Verification

STEP 1: Duplicate Detection
Call query_acumatica_knowledge(query=""<key terms>"", scope=""all"") to check for duplicates.
If DUPLICATE (fuzzy match > 80%) AND exact code location match, skip to STEP 7.
If DUPLICATE but uncertain, continue to STEP 2 for verification.

STEP 2: Scan Code Location (MANDATORY VERIFICATION)
Extract file path and line numbers from the learning.
Use Read tool to scan the file.
Return Scan 1 JSON with foundConstants, foundValidationRules, foundMethods.

STEP 3: Challenge Your Findings
Call challenge_scan_findings tool with:
  scan1Results: ""<your Scan 1 JSON>""
  challengeAreas: [""validation_rules"", ""line_numbers"", ""constants""]

STEP 4: Re-Scan and Verify (SCAN 2)
Execute challenge questions by RE-READING the file.
VERIFY line ranges, constants, validations.
Return Scan 2 Verified Results with scan1Corrections.

STEP 5: Triage Decision
- DUPLICATE: fuzzy match > 80%
- SKIP: customer-specific quirk
- BACKLOG: missing code location
- ADD: general pattern with code reference

STEP 6: Add Knowledge (ONLY if verdict = ADD)
Use Scan 2 Verified Results (not Scan 1).
Read KNOWLEDGE_CONTRIBUTING.md, route to category.
Update operations/<category>.json (ARRAY format).
Increment categories.json count.
Rebuild MCP server.

STEP 7: Output JSON
{{
  ""verdict"": ""ADD|SKIP|DUPLICATE|BACKLOG"",
  ""reason"": ""..."",
  ""duplicateOf"": [""...""],
  ""category"": ""..."",
  ""scan1Results"": {{...}},
  ""scan2Corrections"": [""...""],
  ""filesModified"": [""...""],
  ""operationAdded"": {{...}},
  ""rebuildStatus"": ""success|failed|skipped"",
  ""suggestion"": ""...""
}}

Working directory: /mnt/c/Users/Kosta/source/repos/StampliMCP

CRITICAL ENFORCEMENT:
- MANDATORY: Scan code location (STEP 2)
- MANDATORY: Call challenge_scan_findings (STEP 3)
- MANDATORY: Re-scan and verify (STEP 4)
- MANDATORY: Trust Scan 2 over Scan 1
- Use ARRAY format for operations
- Generate searchKeywords (5-15 keywords)

Begin now.
";

            // Write prompt to temp file (will be piped to Claude CLI via stdin)
            var promptFile = Path.Combine(testDir, "add_knowledge_prompt.txt");
            await File.WriteAllTextAsync(promptFile, prompt);

            Serilog.Log.Information("Wrote prompt to file: {PromptFile} ({Length} chars)", promptFile, prompt.Length);

            // Convert Windows paths to WSL paths
            var wslPromptPath = promptFile.Replace("\\", "/").Replace("C:", "/mnt/c");
            var wslTestDir = testDir.Replace("\\", "/").Replace("C:", "/mnt/c");

            // Spawn Claude CLI using stdin pipe (avoids bash interpretation of prompt content)
            var startInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"bash -c \"MCP_LOG_DIR='{wslTestDir}' cat '{wslPromptPath}' | ~/.local/bin/claude --print --dangerously-skip-permissions\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Serilog.Log.Information("Spawning Claude CLI with file-based prompt: {PromptFile}", wslPromptPath);

            Process? process = null;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception processEx)
            {
                Serilog.Log.Error(processEx, "Exception starting Claude CLI process");
                return new CallToolResult
                {
                    Content = { new TextContentBlock
                    {
                        Type = "text",
                        Text = $"ERROR: Exception starting Claude CLI: {processEx.Message}\n\nStack trace:\n{processEx.StackTrace}"
                    }}
                };
            }

            if (process == null)
            {
                Serilog.Log.Error("Failed to start Claude CLI process");
                return new CallToolResult
                {
                    Content = { new TextContentBlock
                    {
                        Type = "text",
                        Text = "ERROR: Failed to start Claude CLI process (returned null)"
                    }}
                };
            }

            using (process)
            {
                // Close stdin immediately (no input needed with --dangerously-skip-permissions)
                process.StandardInput.Close();

                // Read streams BEFORE waiting (prevents deadlock)
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait with 3 minute timeout (knowledge addition usually takes 30-60 seconds)
                var timeout = TimeSpan.FromMinutes(10); // Increased for two-scan workflow
                var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

                if (!completed)
                {
                    Serilog.Log.Error("Claude CLI timed out after {Timeout} seconds", timeout.TotalSeconds);
                    process.Kill(true);
                    return new CallToolResult
                    {
                        Content = { new TextContentBlock
                        {
                            Type = "text",
                            Text = $"ERROR: Claude CLI timed out after {timeout.TotalSeconds} seconds. Check if process is hung."
                        }}
                    };
                }

                var output = await outputTask;
                var error = await errorTask;

                // Save response to temp directory for debugging
                var responseFile = Path.Combine(testDir, "claude_response.txt");
                var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                                $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                                $"=== STDERR ({error.Length} chars) ===\n{error}\n";
                await File.WriteAllTextAsync(responseFile, logContent);

                // Log results
                Serilog.Log.Information("Claude CLI completed with exit code {ExitCode}", process.ExitCode);
                Serilog.Log.Information("Output length: {Length} chars", output.Length);
                Serilog.Log.Information("Response saved to: {ResponseFile}", responseFile);

                if (!string.IsNullOrEmpty(error))
                {
                    Serilog.Log.Warning("Stderr output: {Error}", error.Substring(0, Math.Min(500, error.Length)));
                }

                // Return result to caller
                var result = new CallToolResult();

                if (process.ExitCode != 0)
                {
                    result.Content.Add(new TextContentBlock
                    {
                        Type = "text",
                        Text = $"ERROR: Claude CLI exited with code {process.ExitCode}\n\nOutput:\n{output}\n\nError:\n{error}"
                    });
                }
                else
                {
                    // Success - return Claude's output
                    result.Content.Add(new TextContentBlock
                    {
                        Type = "text",
                        Text = output
                    });
                }

                Serilog.Log.Information("Tool {Tool} completed successfully", "add_knowledge_from_pr");
                return result;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "add_knowledge_from_pr", ex.Message);
            return new CallToolResult
            {
                Content = { new TextContentBlock
                {
                    Type = "text",
                    Text = $"EXCEPTION: {ex.Message}\n\nStack trace:\n{ex.StackTrace}"
                }}
            };
        }
    }
}
