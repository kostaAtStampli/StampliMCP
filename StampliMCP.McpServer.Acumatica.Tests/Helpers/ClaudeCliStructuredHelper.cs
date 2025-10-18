using System.Diagnostics;
using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Tests.Helpers;

/// <summary>
/// Helper for spawning Claude CLI with structured prompts and extracting JSON from logs
/// </summary>
public static class ClaudeCliStructuredHelper
{
    public static async Task<string> AskClaudeStructured(
        string[] questions,
        string testDir,
        string scanName,
        int timeoutSeconds = 600)
    {
        var prompt = BuildStructuredPrompt(questions);

        // Write prompt to temp file to avoid bash escaping hell
        var promptFile = Path.Combine(testDir, $"{scanName}_prompt.txt");
        File.WriteAllText(promptFile, prompt);
        var wslPromptPath = promptFile.Replace("\\", "/").Replace("C:", "/mnt/c");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"bash -c \"MCP_LOG_DIR='{testDir}' ~/.local/bin/claude --print --dangerously-skip-permissions \\\"$(cat '{wslPromptPath}')\\\"\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Console.WriteLine($"[{scanName}] Executing Claude CLI with {questions.Length} questions...");

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start Claude CLI process");
        }

        // Close stdin immediately
        process.StandardInput.Close();

        // Start reading streams before waiting
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var completed = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds));

        if (!completed)
        {
            process.Kill(true);
            throw new TimeoutException($"Claude CLI timed out after {timeout.TotalSeconds} seconds");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Save to isolated log
        var logPath = Path.Combine(testDir, $"{scanName}_output.txt");
        var logContent = $"=== Exit Code: {process.ExitCode} ===\n" +
                        $"=== STDOUT ({output.Length} chars) ===\n{output}\n" +
                        $"=== STDERR ({error.Length} chars) ===\n{error}\n";
        File.WriteAllText(logPath, logContent);

        Console.WriteLine($"[{scanName}] Completed. Exit code: {process.ExitCode}. Log: {logPath}");

        if (process.ExitCode != 0)
        {
            throw new Exception($"Claude CLI failed with exit code {process.ExitCode}. Check log: {logPath}");
        }

        return logPath;
    }

    public static string ExtractJsonFromLog(string logPath)
    {
        var content = File.ReadAllText(logPath);

        // Extract JSON between STDOUT markers
        var stdoutStart = content.IndexOf("=== STDOUT", StringComparison.Ordinal);
        var stderrStart = content.IndexOf("=== STDERR", StringComparison.Ordinal);

        if (stdoutStart == -1)
        {
            throw new InvalidOperationException($"No STDOUT section found in log: {logPath}");
        }

        var stdoutContent = stderrStart > stdoutStart
            ? content.Substring(stdoutStart, stderrStart - stdoutStart)
            : content.Substring(stdoutStart);

        // Find JSON (starts with { or [)
        var jsonStart = -1;
        for (int i = 0; i < stdoutContent.Length; i++)
        {
            if (stdoutContent[i] == '{' || stdoutContent[i] == '[')
            {
                jsonStart = i;
                break;
            }
        }

        if (jsonStart == -1)
        {
            // No JSON found - return the whole STDOUT section (Claude may have returned text)
            Console.WriteLine($"Warning: No JSON found in log. Returning raw STDOUT.");
            return stdoutContent;
        }

        var json = stdoutContent.Substring(jsonStart).Trim();

        // Strip trailing markdown fence if present (JSON may end with ```)
        var closingFence = json.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence > 0)
        {
            // Remove everything from closing fence onwards
            json = json.Substring(0, closingFence).Trim();
        }

        return json;
    }

    public static void ValidateStructuredResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            Console.WriteLine($"✓ Valid JSON ({json.Length} chars)");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON response: {ex.Message}\nJSON:\n{json}");
        }
    }

    private static string BuildStructuredPrompt(string[] questions)
    {
        return $@"
You are a code analysis expert. Answer the following questions by scanning C:\STAMPLI4 files.

CRITICAL RULES:
1. Return ONLY valid JSON (no markdown, no explanations outside JSON)
2. Use Windows paths (C:\STAMPLI4\...) not WSL paths (/mnt/c/)
3. Be PRECISE with line numbers
4. If you find something different than expected, REPORT THE ACTUAL VALUE

Questions:
{string.Join("\n\n", questions.Select((q, i) => $"Q{i + 1}: {q}"))}

Return JSON array:
[
  {{ ""question"": 1, ""answer"": {{ ... }} }},
  {{ ""question"": 2, ""answer"": {{ ... }} }},
  ...
]
";
    }

}
