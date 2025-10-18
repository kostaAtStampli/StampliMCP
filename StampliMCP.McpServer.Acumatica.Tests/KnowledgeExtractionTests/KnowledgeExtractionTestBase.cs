using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica.Tests.Helpers;

namespace StampliMCP.McpServer.Acumatica.Tests.KnowledgeExtractionTests;

/// <summary>
/// Base class for knowledge extraction tests that enforces two-scan pattern using MCP tools
/// </summary>
public abstract class KnowledgeExtractionTestBase
{
    /// <summary>
    /// Runs the two-scan extraction pattern with MCP tool integration
    /// </summary>
    protected async Task<ExtractedKnowledge> RunTwoScanExtraction(
        string area,
        string[] scan1Questions,
        string[] challengeAreas,
        string testDir)
    {
        Console.WriteLine($"\n=== Running Two-Scan Extraction for {area} ===");
        Console.WriteLine($"Test directory: {testDir}");

        // ═══ STEP 1: Run Scan 1 (Broad Discovery) ═══
        Console.WriteLine($"\n=== SCAN 1: {area} Discovery ===");
        Console.WriteLine($"Questions: {scan1Questions.Length}");

        var scan1LogPath = await ClaudeCliStructuredHelper.AskClaudeStructured(
            scan1Questions,
            testDir,
            $"scan1_{area}");

        // Extract JSON from Scan 1
        var scan1Json = ClaudeCliStructuredHelper.ExtractJsonFromLog(scan1LogPath);
        Console.WriteLine($"Scan 1 completed. JSON extracted: {scan1Json.Length} chars");

        // ═══ STEP 2: Call MCP Tool to Generate Scan 2 Challenges ═══
        Console.WriteLine($"\n=== Calling MCP Tool: challenge_scan_findings ===");
        Console.WriteLine($"Challenge areas: {string.Join(", ", challengeAreas)}");

        // Write scan1 results to a file to avoid bash escaping issues
        var scan1ResultsFile = Path.Combine(testDir, "scan1_results.json");
        await File.WriteAllTextAsync(scan1ResultsFile, scan1Json);

        // Build challenge areas array properly
        var challengeAreasJson = "[" + string.Join(", ", challengeAreas.Select(a => $"\"{a}\"")) + "]";

        // Build the MCP prompt with file reference
        var mcpPrompt = $@"
Call the MCP tool 'challenge_scan_findings' from stampli-acumatica server with these exact arguments:

1. Read the scan1Results from this file: {scan1ResultsFile.Replace("\\", "/")}
2. Use these challengeAreas: {challengeAreasJson}

The tool should generate 7 skeptical verification questions to challenge Scan 1 findings.
Return the challenge questions as a numbered list.
";

        var challengeResult = await CallMcpToolDirect(
            mcpPrompt,
            testDir,
            "challenge_scan_findings",
            area);

        Console.WriteLine($"MCP tool generated {challengeResult.ChallengeCount} challenge questions");
        Console.WriteLine($"Scan 1 summary: {challengeResult.Scan1Summary}");

        // ═══ STEP 3: Run Scan 2 (Deep Verification with Challenges) ═══
        Console.WriteLine($"\n=== SCAN 2: {area} Challenge & Verification ===");

        var scan2LogPath = await ClaudeCliStructuredHelper.AskClaudeStructured(
            challengeResult.ChallengeQuestions,
            testDir,
            $"scan2_{area}");

        Console.WriteLine($"Scan 2 completed. Path: {scan2LogPath}");

        // ═══ STEP 4: Parse Both Scans & Extract Knowledge ═══
        Console.WriteLine($"\n=== Parsing Logs & Extracting Knowledge ===");

        var extractor = new KnowledgeExtractor();
        var knowledge = extractor.ParseLogs(scan1LogPath, scan2LogPath);

        Console.WriteLine($"✓ Extracted knowledge for {area}:");
        Console.WriteLine($"  - Required fields: {knowledge.RequiredFields.Count}");
        Console.WriteLine($"  - Files to scan: {knowledge.ScanThese.Count}");
        Console.WriteLine($"  - Kotlin files: {knowledge.KotlinFiles.Count}");
        Console.WriteLine($"  - Scan 2 challenges/corrections: {knowledge.Scan2Challenges}");

        return knowledge;
    }

    /// <summary>
    /// Calls Claude with a direct prompt and returns ChallengeScanResult
    /// </summary>
    protected async Task<ChallengeScanResult> CallMcpToolDirect(string prompt, string testDir, string toolName, string area = "operations")
    {
        Console.WriteLine($"Calling MCP via direct prompt for: {toolName}");

        // Write prompt to temp file
        var promptFile = Path.Combine(testDir, $"mcp_tool_{toolName}_prompt.txt");
        await File.WriteAllTextAsync(promptFile, prompt);

        // Convert paths for WSL
        var wslPromptPath = promptFile.Replace("\\", "/").Replace("C:", "/mnt/c");
        var wslTestDir = testDir.Replace("\\", "/").Replace("C:", "/mnt/c");

        // Execute via Claude CLI
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"MCP_LOG_DIR='{wslTestDir}' ~/.local/bin/claude --print --dangerously-skip-permissions \\\"$(cat '{wslPromptPath}')\\\"\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull($"Claude CLI process should start for {toolName}");

        process!.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(300);
        var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process.Kill(true);
            throw new TimeoutException($"MCP call {toolName} timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Save response
        var logPath = Path.Combine(testDir, $"mcp_tool_{toolName}_response.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        await File.WriteAllTextAsync(logPath, logContent);

        if (process.ExitCode != 0)
        {
            throw new Exception($"MCP call {toolName} failed with exit code {process.ExitCode}. Check log: {logPath}");
        }

        // Extract challenge questions from response
        // Look for numbered questions (1. 2. etc.) or Q1: Q2: format
        var lines = output.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var questions = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Match numbered questions like "1." "2." or "Q1:" "Q2:"
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(\d+\.|Q\d+:)"))
            {
                questions.Add(trimmed);
            }
            // Also capture lines that look like questions (start with common question words)
            else if (trimmed.StartsWith("What ") || trimmed.StartsWith("How many ") ||
                     trimmed.StartsWith("Which ") || trimmed.StartsWith("Verify ") ||
                     trimmed.StartsWith("Count ") || trimmed.StartsWith("Find "))
            {
                questions.Add(trimmed);
            }
        }

        // If we didn't find structured questions, take the first 7 non-empty lines
        if (questions.Count == 0)
        {
            questions = lines.Take(7).ToList();
        }

        return new ChallengeScanResult
        {
            Scan1Summary = $"Scan 1 found {area} operations",
            ChallengeQuestions = questions.Take(7).ToArray(),
            ChallengeCount = Math.Min(7, questions.Count),
            Instruction = "Verify Scan 1 findings with these challenge questions"
        };
    }

    /// <summary>
    /// Calls an MCP tool via Claude CLI and returns structured result
    /// </summary>
    protected async Task<T> CallMcpTool<T>(string toolName, object args, string testDir)
        where T : new()
    {
        Console.WriteLine($"Calling MCP tool: {toolName}");

        // Build prompt to call MCP tool
        var argsJson = JsonSerializer.Serialize(args, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var prompt = $@"
Call the MCP tool '{toolName}' with these exact arguments and return ONLY the tool's JSON response (no markdown, no explanation):
{argsJson}

CRITICAL: Return ONLY the raw JSON response from the tool, nothing else.
";

        // Write prompt to temp file
        var promptFile = Path.Combine(testDir, $"mcp_tool_{toolName}_prompt.txt");
        await File.WriteAllTextAsync(promptFile, prompt);

        // Convert paths for WSL
        var wslPromptPath = promptFile.Replace("\\", "/").Replace("C:", "/mnt/c");
        var wslTestDir = testDir.Replace("\\", "/").Replace("C:", "/mnt/c");

        // Execute via Claude CLI - using cat to avoid escaping issues
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"MCP_LOG_DIR='{wslTestDir}' ~/.local/bin/claude --print --dangerously-skip-permissions \\\"$(cat '{wslPromptPath}')\\\"\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull($"Claude CLI process should start for MCP tool {toolName}");

        process!.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(300); // 5 minutes for MCP tool call
        var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process.Kill(true);
            throw new TimeoutException($"MCP tool call {toolName} timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Save MCP tool response
        var logPath = Path.Combine(testDir, $"mcp_tool_{toolName}_response.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        await File.WriteAllTextAsync(logPath, logContent);

        if (process.ExitCode != 0)
        {
            throw new Exception($"MCP tool {toolName} failed with exit code {process.ExitCode}. Check log: {logPath}");
        }

        // Extract JSON from response
        var jsonResponse = ExtractJsonFromMcpResponse(output);

        // Parse and return structured result
        try
        {
            var result = JsonSerializer.Deserialize<T>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new T();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Could not parse MCP tool response as {typeof(T).Name}: {ex.Message}");
            Console.WriteLine($"Raw JSON: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}...");
            return new T(); // Return default instance
        }
    }

    /// <summary>
    /// Extracts JSON from MCP tool response (strips markdown if present)
    /// </summary>
    private string ExtractJsonFromMcpResponse(string response)
    {
        // Find first { or [
        var jsonStart = -1;
        for (int i = 0; i < response.Length; i++)
        {
            if (response[i] == '{' || response[i] == '[')
            {
                jsonStart = i;
                break;
            }
        }

        if (jsonStart == -1)
        {
            throw new InvalidOperationException("No JSON found in MCP tool response");
        }

        var json = response.Substring(jsonStart).Trim();

        // Strip trailing markdown fence if present
        var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence > 0)
        {
            json = json.Substring(0, closingFence).Trim();
        }

        return json;
    }

    /// <summary>
    /// Updates a knowledge JSON file with extracted knowledge
    /// </summary>
    protected void UpdateKnowledgeFile(string fileName, ExtractedKnowledge knowledge)
    {
        var knowledgePath = Path.Combine(
            "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\Knowledge",
            fileName);

        Console.WriteLine($"\n=== Updating Knowledge File: {fileName} ===");

        // Create the file if it doesn't exist
        if (!File.Exists(knowledgePath))
        {
            var initialContent = JsonSerializer.Serialize(new
            {
                purpose = $"Knowledge for {Path.GetFileNameWithoutExtension(fileName)}",
                usage = "Auto-extracted from code scanning",
                lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                data = knowledge
            }, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(knowledgePath, initialContent);
            Console.WriteLine($"✓ Created new knowledge file: {knowledgePath}");
        }
        else
        {
            var extractor = new KnowledgeExtractor();
            extractor.UpdateKnowledgeFile(knowledgePath, knowledge);
            Console.WriteLine($"✓ Updated existing knowledge file: {knowledgePath}");
        }

        Console.WriteLine($"  - Required fields: {knowledge.RequiredFields.Count}");
        Console.WriteLine($"  - Files to scan: {knowledge.ScanThese.Count}");
        Console.WriteLine($"  - Scan 2 corrections: {knowledge.Scan2Challenges}");
    }

    /// <summary>
    /// Creates an isolated test directory
    /// </summary>
    protected string CreateTestDirectory(string testName)
    {
        var testDir = Path.Combine(
            Path.GetTempPath(),
            $"mcp_{testName}_extraction_{DateTime.Now:yyyyMMdd_HHmmss}");

        Directory.CreateDirectory(testDir);
        Console.WriteLine($"=== Test directory created: {testDir} ===");

        return testDir;
    }

    /// <summary>
    /// Override to provide Scan 1 questions for specific area
    /// </summary>
    protected abstract string[] GetScan1Questions();

    /// <summary>
    /// Override to provide challenge areas for MCP tool
    /// </summary>
    protected virtual string[] GetChallengeAreas()
    {
        // Default challenge areas
        return new[]
        {
            "validation_rules",
            "line_numbers",
            "constants",
            "operation_count"
        };
    }
}