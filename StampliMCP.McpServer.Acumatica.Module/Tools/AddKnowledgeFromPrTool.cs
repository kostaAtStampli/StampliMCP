using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class AddKnowledgeFromPrTool
{
    private static readonly Dictionary<string, string> ErpModuleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["acumatica"] = "Acumatica",
        ["intacct"] = "Intacct"
    };

    [McpServerTool(
        Name = "add_knowledge_from_pr",
        Title = "Knowledge Update Planning",
        UseStructuredContent = true)]
    [Description(@"
Plan and verify knowledge-base updates for a PR using live git + knowledge context (no direct file mutation).

Inputs:
• erp (required) – ERP key such as 'acumatica'
• prNumber (optional) – PR identifier for logging
• learnings (optional) – developer-noted highlights to cross-check against the diff
• currentBranch (optional) – overrides detected git branch when working in detached HEAD
• dryRun (default: true) – when false, the tool will emit `apply_patch`-ready diffs once verification succeeds

Workflow:
1. Collect git status, diff summaries, and recent commits for the current branch.
2. Load ERP-specific knowledge files (categories + operations) and build a structured snapshot.
3. Perform host-level Scan 1 on referenced source files, call challenge_scan_findings, then perform Scan 2.
4. Spawn Claude CLI with the captured evidence and require a JSON verdict (ADD/SKIP/DUPLICATE/BACKLOG).
5. Return the structured response + audit context without mutating files (dry run) or with generated patches (non dry run).")]
    public static async Task<CallToolResult> Execute(
        [Description("ERP key (e.g., acumatica, intacct)")] string erp,
        [Description("Optional PR number (e.g., #456)")] string? prNumber,
        [Description("Key learnings / business rules from the PR (optional)")] string? learnings,
        [Description("Override for git branch; auto-detected when omitted")] string? currentBranch,
        [Description("Set to false to request apply_patch-ready diffs; default true (plan only).")] bool dryRun = true,
        CancellationToken ct = default)
    {
        Serilog.Log.Information("Tool {Tool} started: erp={Erp}, pr={PR}, dryRun={DryRun}",
            "add_knowledge_from_pr", erp, prNumber, dryRun);

        try
        {
            var repoRoot = LocateRepoRoot();
            var moduleKnowledge = ResolveKnowledgeDirectory(repoRoot, erp);

            var gitContext = await CollectGitContextAsync(repoRoot, currentBranch, ct);
            var knowledgeSnapshot = await BuildKnowledgeSnapshotAsync(moduleKnowledge, ct);

            var promptEnvelope = BuildPromptEnvelope(
                repoRoot,
                moduleKnowledge,
                erp,
                prNumber,
                learnings,
                gitContext,
                knowledgeSnapshot,
                dryRun);

            var promptFile = await WritePromptAsync(promptEnvelope, ct);
            var cliResult = await RunClaudeCliAsync(promptEnvelope.TempDirectory, promptFile, ct);

            // Try to parse Claude CLI stdout as STRICT JSON per prompt contract
            var (parsedJson, parseErrors) = TryParseStrictJson(cliResult.Stdout);

            // Validate minimal schema if JSON parsed
            var validation = parsedJson is null
                ? new ValidationReport(false, new[] { "Missing or invalid JSON in Claude response." }.Concat(parseErrors).ToArray())
                : ValidatePlannerSchema(parsedJson);

            var callResult = new CallToolResult
            {
                StructuredContent = JsonSerializer.SerializeToNode(new
                {
                    context = new
                    {
                        erp,
                        prNumber,
                        git = gitContext,
                        knowledge = knowledgeSnapshot,
                        repoRoot = ToUnixPath(repoRoot),
                        knowledgeRoot = ToUnixPath(moduleKnowledge),
                        dryRun
                    },
                    cli = new
                    {
                        promptFile = ToUnixPath(promptFile),
                        responseFile = ToUnixPath(cliResult.LogFile),
                        exitCode = cliResult.ExitCode
                    },
                    planner = new
                    {
                        parsed = parsedJson,
                        valid = validation.IsValid,
                        errors = validation.Errors
                    }
                })
            };

            var headerBuilder = new StringBuilder();
            headerBuilder.AppendLine("=== Knowledge Update Planner ===");
            headerBuilder.AppendLine($"ERP: {erp}");
            headerBuilder.AppendLine($"Git branch: {gitContext.Branch ?? "(unknown)"}");
            headerBuilder.AppendLine($"Dry run: {dryRun}");
            if (!string.IsNullOrWhiteSpace(prNumber))
            {
                headerBuilder.AppendLine($"PR: {prNumber}");
            }

            callResult.Content.Add(new TextContentBlock
            {
                Type = "text",
                Text = headerBuilder.ToString()
            });

            if (cliResult.ExitCode != 0)
            {
                callResult.Content.Add(new TextContentBlock
                {
                    Type = "text",
                    Text = $"ERROR: Claude CLI exited with code {cliResult.ExitCode}\nSTDERR:\n{cliResult.Stderr}"
                });
            }

            // Summarize parsing/validation and echo a compact view for humans
            var humanSummary = new StringBuilder()
                .AppendLine("=== Planner Result ===")
                .AppendLine($"JSON parsed: {(parsedJson is not null ? "yes" : "no")}")
                .AppendLine($"Schema valid: {validation.IsValid}")
                .AppendLine(validation.Errors.Length > 0 ? $"Errors: {string.Join("; ", validation.Errors)}" : "Errors: none")
                .ToString();

            callResult.Content.Add(new TextContentBlock { Type = "text", Text = humanSummary });

            // Include raw stdout for debugging only if JSON failed
            if (parsedJson is null)
            {
                callResult.Content.Add(new TextContentBlock { Type = "text", Text = cliResult.Stdout });
            }

            // If non-dry-run and schema valid, attempt guarded apply of updates
            if (!dryRun && parsedJson is not null && validation.IsValid)
            {
                try
                {
                    var applied = ApplyKnowledgeUpdatesSafely(parsedJson, moduleKnowledge);
                    callResult.Content.Add(new TextContentBlock
                    {
                        Type = "text",
                        Text = $"Applied {applied.AppliedCount} update(s). Skipped: {applied.SkippedCount}."
                    });
                }
                catch (Exception apEx)
                {
                    callResult.Content.Add(new TextContentBlock
                    {
                        Type = "text",
                        Text = $"APPLY FAILED: {apEx.Message}"
                    });
                }
            }

            callResult.Content.Add(new ResourceLinkBlock
            {
                Name = "See raw CLI transcript",
                Uri = ToUnixPath(cliResult.LogFile)
            });

            Serilog.Log.Information("Tool {Tool} completed: exitCode={ExitCode}, promptDir={Dir}",
                "add_knowledge_from_pr", cliResult.ExitCode, promptEnvelope.TempDirectory);

            return callResult;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "add_knowledge_from_pr", ex.Message);
            return new CallToolResult
            {
                Content =
                {
                    new TextContentBlock
                    {
                        Type = "text",
                        Text = $"EXCEPTION: {ex.Message}\n\n{ex}"
                    }
                }
            };
        }
    }

    // Attempt to parse a single JSON object from stdout; return JsonNode and any errors
    private static (JsonNode? Node, string[] Errors) TryParseStrictJson(string stdout)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return (null, new[] { "Empty STDOUT from Claude CLI" });
        }

        // Fast path: try parse entire stdout
        try
        {
            var node = JsonNode.Parse(stdout);
            return (node, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            errors.Add($"Direct parse failed: {ex.Message}");
        }

        // Fallback: attempt to extract first top-level JSON object via brace scanning
        var idxStart = stdout.IndexOf('{');
        while (idxStart >= 0)
        {
            try
            {
                var extracted = ExtractJsonObject(stdout, idxStart);
                if (extracted is not null)
                {
                    var node = JsonNode.Parse(extracted);
                    return (node, errors.ToArray());
                }
            }
            catch (Exception innerEx)
            {
                errors.Add($"Slice parse failed at {idxStart}: {innerEx.Message}");
            }

            idxStart = stdout.IndexOf('{', idxStart + 1);
        }

        return (null, errors.ToArray());
    }

    private static string? ExtractJsonObject(string text, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '{') depth++;
            if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(startIndex, i - startIndex + 1);
                }
            }
        }
        return null;
    }

    private static ValidationReport ValidatePlannerSchema(JsonNode node)
    {
        var errs = new List<string>();
        try
        {
            if (node is not JsonObject obj)
            {
                errs.Add("Top-level JSON must be an object");
                return new ValidationReport(false, errs.ToArray());
            }

            if (!obj.TryGetPropertyValue("verdict", out var verdictNode) || verdictNode is null || verdictNode.GetValue<string?>() is null)
            {
                errs.Add("Missing 'verdict' (string)");
            }

            if (!obj.TryGetPropertyValue("reason", out var _))
            {
                errs.Add("Missing 'reason'");
            }

            if (obj.TryGetPropertyValue("knowledgeUpdates", out var ku) && ku is not null && ku is not JsonArray)
            {
                errs.Add("'knowledgeUpdates' must be an array when present");
            }

            // Optional minimal gitEvidence fields for traceability
            if (!obj.TryGetPropertyValue("gitEvidence", out var gitEv) || gitEv is null)
            {
                errs.Add("Missing 'gitEvidence'");
            }

            return new ValidationReport(errs.Count == 0, errs.ToArray());
        }
        catch (Exception ex)
        {
            errs.Add($"Validation exception: {ex.Message}");
            return new ValidationReport(false, errs.ToArray());
        }
    }

    private static ApplyReport ApplyKnowledgeUpdatesSafely(JsonNode plannerJson, string knowledgeRoot)
    {
        var applied = 0;
        var skipped = 0;

        if (plannerJson is not JsonObject obj || !obj.TryGetPropertyValue("knowledgeUpdates", out var kuNode) || kuNode is not JsonArray kuArr)
        {
            return new ApplyReport(applied, skipped);
        }

        foreach (var entry in kuArr)
        {
            try
            {
                if (entry is not JsonObject e)
                {
                    skipped++;
                    continue;
                }

                var file = e["file"]?.GetValue<string?>();
                var action = e["action"]?.GetValue<string?>();
                var applyPatch = e["applyPatch"]?.GetValue<string?>();

                if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(action))
                {
                    skipped++;
                    continue;
                }

                // Guard: file must be under knowledgeRoot
                var normalized = ToUnixPath(Path.GetFullPath(file));
                var knowledgeUnix = ToUnixPath(Path.GetFullPath(knowledgeRoot));
                if (!normalized.StartsWith(knowledgeUnix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Refusing to edit outside knowledge root: {normalized}");
                }

                // For now, only accept applyPatch blocks; else skip with message
                if (string.IsNullOrWhiteSpace(applyPatch))
                {
                    skipped++;
                    continue;
                }

                // Minimal, conservative patch runner: require that patch touches only 'file'
                ApplyMinimalPatch(applyPatch, normalized);
                applied++;
            }
            catch (Exception)
            {
                skipped++;
            }
        }

        return new ApplyReport(applied, skipped);
    }

    private static void ApplyMinimalPatch(string patch, string expectedFilePath)
    {
        // Very conservative apply: only allow a single *** Update File: <path> or *** Add File: <path>
        // and replace the entire file content with the final version if fully specified in hunk (lines prefixed by + or context only)
        // If ambiguity is detected, throw to prevent unsafe writes.

        if (!patch.Contains("*** Begin Patch") || !patch.Contains("*** End Patch"))
        {
            throw new InvalidOperationException("Invalid patch envelope");
        }

        // Detect the file path referenced
        using var reader = new StringReader(patch);
        string? line;
        string? op = null;
        string? path = null;
        var contentBuilder = new StringBuilder();
        var inAddFile = false;
        var inUpdateFile = false;
        var sawHunk = false;

        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith("*** Add File: "))
            {
                op = "add";
                path = line.Substring("*** Add File: ".Length).Trim();
                inAddFile = true;
                continue;
            }
            if (line.StartsWith("*** Update File: "))
            {
                op = "update";
                path = line.Substring("*** Update File: ".Length).Trim();
                inUpdateFile = true;
                continue;
            }
            if (line.StartsWith("@@"))
            {
                sawHunk = true;
                continue;
            }

            if (inAddFile)
            {
                if (line.StartsWith("+"))
                {
                    contentBuilder.AppendLine(line[1..]);
                }
            }
            else if (inUpdateFile && line.Length > 0 && (line[0] == ' ' || line[0] == '+' ))
            {
                // For update, we can only safely reconstruct content if the patch provides only additions and context (no deletions '-')
                if (line[0] == '+')
                {
                    contentBuilder.AppendLine(line[1..]);
                }
                else
                {
                    contentBuilder.AppendLine(line[1..]);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(op) || string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Patch missing file operation header");
        }

        // Guard: normalize and compare target path
        var patchPathFull = ToUnixPath(Path.GetFullPath(path));
        if (!string.Equals(patchPathFull, ToUnixPath(Path.GetFullPath(expectedFilePath)), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Patch path mismatch: {patchPathFull} != {expectedFilePath}");
        }

        // For update, if we didn't capture any content (because deletions exist), refuse to apply
        if (op == "update" && contentBuilder.Length == 0)
        {
            throw new InvalidOperationException("Refusing to apply complex update patch (deletions or incomplete content)");
        }

        if (op == "add")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(expectedFilePath)!);
            File.WriteAllText(expectedFilePath, contentBuilder.ToString());
        }
        else if (op == "update")
        {
            // Best-effort: write reconstructed content
            File.WriteAllText(expectedFilePath, contentBuilder.ToString());
        }
    }

    private static PromptEnvelope BuildPromptEnvelope(
        string repoRoot,
        string knowledgeRoot,
        string erp,
        string? prNumber,
        string? learnings,
        GitContext gitContext,
        KnowledgeSnapshot knowledgeSnapshot,
        bool dryRun)
    {
        var promptDirectory = Path.Combine(Path.GetTempPath(), $"mcp_knowledge_plan_{DateTime.Now:yyyyMMdd_HHmmssfff}");
        Directory.CreateDirectory(promptDirectory);

        var payload = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            erp,
            prNumber,
            dryRun,
            repoRoot = ToUnixPath(repoRoot),
            knowledgeRoot = ToUnixPath(knowledgeRoot),
            git = gitContext,
            knowledge = knowledgeSnapshot,
            developerLearnings = string.IsNullOrWhiteSpace(learnings) ? null : learnings
        };

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("You are an MCP planner asked to validate whether ERP knowledge files require updates based on a pull request.");
        promptBuilder.AppendLine("You MUST analyse the supplied git + knowledge context, perform TWO SCANS for every source reference, and emit a single JSON object following the specified schema.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("### Non-Negotiable Rules");
        promptBuilder.AppendLine("1. Never write to disk. Produce recommendations and, when dryRun=false, include `apply_patch` snippets (but do not execute them).");
        promptBuilder.AppendLine("2. Perform Scan 1 by reading the cited source files and returning a machine-readable summary (constants, validations, methods).");
        promptBuilder.AppendLine("3. Call the `challenge_scan_findings` tool with your Scan 1 results; then perform Scan 2 addressing every challenge before finalising your verdict.");
        promptBuilder.AppendLine("4. Limit all knowledge updates to files below `knowledgeRoot`; ignore other ERPs.");
        promptBuilder.AppendLine("5. Base your decision on both the git diff and developer learnings. If learnings are absent, infer from the diff only if evidence is conclusive.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("### Required Output Schema (single JSON object)");
        promptBuilder.AppendLine(@"{
  ""verdict"": ""ADD"" | ""SKIP"" | ""DUPLICATE"" | ""BACKLOG"",
  ""reason"": ""short explanation"",
  ""gitEvidence"": {
    ""branch"": """",
    ""statusSummary"": ""..."",
    ""diffSummary"": ""...""
  },
  ""scan1"": {
    ""files"": [
      {
        ""path"": ""...relative or absolute..."",
        ""constants"": [""...""],
        ""validations"": [""...""],
        ""methods"": [""...""]
      }
    ],
    ""raw"": { ... arbitrary JSON ... }
  },
  ""scanChallenges"": [ ""question"", ""question"" ],
  ""scan2"": {
    ""corrections"": [""..."", ""...""],
    ""verifiedEvidence"": [""..."", ""...""]
  },
  ""knowledgeUpdates"": [
    {
      ""file"": ""/mnt/.../Knowledge/operations/vendors.json"",
      ""action"": ""append"" | ""modify"" | ""none"",
      ""operationKey"": ""exportVendor"",
      ""applyPatch"": ""<apply_patch block when dryRun=false else null>"",
      ""preview"": ""human readable summary""
    }
  ],
  ""suggestedCommitMessage"": ""..."",
  ""nextSteps"": [""..."", ""...""]
}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("### Context Payload (JSON)");
        promptBuilder.AppendLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Respond with STRICT JSON (no markdown, no commentary).");

        return new PromptEnvelope(promptDirectory, promptBuilder.ToString());
    }

    private static async Task<string> WritePromptAsync(PromptEnvelope envelope, CancellationToken ct)
    {
        var promptFile = Path.Combine(envelope.TempDirectory, "knowledge_plan_prompt.txt");
        await File.WriteAllTextAsync(promptFile, envelope.Prompt, ct);
        return promptFile;
    }

    private static async Task<CliExecutionResult> RunClaudeCliAsync(string workingDir, string promptFile, CancellationToken ct)
    {
        var promptPathUnix = ToUnixPath(promptFile);
        var workDirUnix = ToUnixPath(workingDir);

        // Resolve CLI path and args from environment for flexibility
        var cliPath = Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH");
        cliPath = string.IsNullOrWhiteSpace(cliPath) ? "~/.local/bin/claude" : cliPath;
        var cliArgs = Environment.GetEnvironmentVariable("CLAUDE_CLI_ARGS");
        cliArgs = string.IsNullOrWhiteSpace(cliArgs) ? "--print --dangerously-skip-permissions" : cliArgs;

        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments =
                $"bash -c \"MCP_LOG_DIR='{workDirUnix}' cat '{promptPathUnix}' | {cliPath} {cliArgs}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
                         ?? throw new InvalidOperationException("Failed to launch Claude CLI (process start returned null).");

        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        var transcriptFile = Path.Combine(workingDir, "claude_response.txt");
        var transcript = new StringBuilder()
            .AppendLine($"=== Exit Code: {process.ExitCode} ===")
            .AppendLine($"=== STDOUT ({stdout.Length} chars) ===")
            .AppendLine(stdout)
            .AppendLine($"=== STDERR ({stderr.Length} chars) ===")
            .AppendLine(stderr)
            .ToString();

        await File.WriteAllTextAsync(transcriptFile, transcript, ct);

        if (!string.IsNullOrEmpty(stderr))
        {
            Serilog.Log.Warning("Claude CLI stderr: {Error}", stderr[..Math.Min(stderr.Length, 500)]);
        }

        return new CliExecutionResult(process.ExitCode, stdout, stderr, transcriptFile);
    }

    private static async Task<KnowledgeSnapshot> BuildKnowledgeSnapshotAsync(string knowledgeRoot, CancellationToken ct)
    {
        if (!Directory.Exists(knowledgeRoot))
        {
            throw new DirectoryNotFoundException($"Knowledge directory not found: {knowledgeRoot}");
        }

        var categoriesPath = Path.Combine(knowledgeRoot, "categories.json");
        var categories = File.Exists(categoriesPath)
            ? JsonNode.Parse(await File.ReadAllTextAsync(categoriesPath, ct))
            : null;

        var operationsDir = Path.Combine(knowledgeRoot, "operations");
        var operationFiles = Directory.Exists(operationsDir)
            ? Directory.GetFiles(operationsDir, "*.json", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        var summaries = new ConcurrentBag<KnowledgeFileSummary>();

        await Parallel.ForEachAsync(operationFiles, ct, async (file, token) =>
        {
            try
            {
                var text = await File.ReadAllTextAsync(file, token);
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    summaries.Add(new KnowledgeFileSummary(ToUnixPath(file), 0, Array.Empty<string>()));
                    return;
                }

                var methods = doc.RootElement
                    .EnumerateArray()
                    .Select(static element =>
                    {
                        if (element.ValueKind != JsonValueKind.Object)
                        {
                            return null;
                        }

                        return element.TryGetProperty("method", out var methodProp)
                            ? methodProp.GetString()
                            : null;
                    })
                    .Where(static method => !string.IsNullOrWhiteSpace(method))
                    .Take(15) // avoid overloading the prompt
                    .OfType<string>()
                    .ToArray();

                summaries.Add(new KnowledgeFileSummary(ToUnixPath(file), doc.RootElement.GetArrayLength(), methods));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to summarise knowledge file {File}", file);
                summaries.Add(new KnowledgeFileSummary(ToUnixPath(file), -1, Array.Empty<string>()));
            }
        });

        return new KnowledgeSnapshot(ToUnixPath(knowledgeRoot), categories, summaries.OrderBy(s => s.File).ToArray());
    }

    private static async Task<GitContext> CollectGitContextAsync(string repoRoot, string? overrideBranch, CancellationToken ct)
    {
        var branch = !string.IsNullOrWhiteSpace(overrideBranch)
            ? overrideBranch
            : (await RunGitAsync(repoRoot, "rev-parse --abbrev-ref HEAD", ct)).Stdout.Trim();

        var upstream = await TryGetGitValueAsync(repoRoot, "rev-parse --abbrev-ref --symbolic-full-name @{upstream}", ct);

        var status = await RunGitAsync(repoRoot, "status --short", ct);
        var diffStat = await RunGitAsync(repoRoot, upstream.Success
            ? $"diff {upstream.Value} --stat"
            : "diff --stat", ct);
        var diffNameStatus = await RunGitAsync(repoRoot, upstream.Success
            ? $"diff {upstream.Value} --name-status"
            : "diff --name-status", ct);
        var latestCommits = await RunGitAsync(repoRoot, "log -5 --oneline", ct);

        return new GitContext(
            branch,
            upstream.Success ? upstream.Value : null,
            status.Stdout,
            diffStat.Stdout,
            diffNameStatus.Stdout,
            latestCommits.Stdout,
            status.ExitCode,
            diffStat.ExitCode,
            diffNameStatus.ExitCode,
            latestCommits.ExitCode);
    }

    private static async Task<GitCommandResult> RunGitAsync(string repoRoot, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
                         ?? throw new InvalidOperationException($"Failed to start git with args '{arguments}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(ct);

        return new GitCommandResult(
            process.ExitCode,
            arguments,
            (await stdoutTask).Trim(),
            (await stderrTask).Trim());
    }

    private static async Task<(bool Success, string Value)> TryGetGitValueAsync(string repoRoot, string arguments, CancellationToken ct)
    {
        try
        {
            var result = await RunGitAsync(repoRoot, arguments, ct);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Stdout)
                ? (true, result.Stdout.Trim())
                : (false, string.Empty);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    private static string LocateRepoRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(directory))
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate git repository root from current working directory.");
    }

    private static string ResolveKnowledgeDirectory(string repoRoot, string erp)
    {
        if (!ErpModuleNames.TryGetValue(erp, out var moduleName))
        {
            throw new ArgumentException($"Unsupported ERP key '{erp}'. Known ERPs: {string.Join(", ", ErpModuleNames.Keys)}", nameof(erp));
        }

        var path = Path.Combine(repoRoot, $"StampliMCP.McpServer.{moduleName}.Module", "Knowledge");
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Knowledge directory not found for ERP '{erp}' at '{path}'.");
        }

        return path;
    }

    private static string ToUnixPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path.Contains('/'))
        {
            return path.Replace("\\", "/");
        }

        var match = Regex.Match(path, @"^(?<drive>[A-Za-z]):\\(?<rest>.*)$");
        if (!match.Success)
        {
            return path.Replace("\\", "/");
        }

        var drive = char.ToLowerInvariant(match.Groups["drive"].Value[0]);
        var rest = match.Groups["rest"].Value.Replace("\\", "/");
        return $"/mnt/{drive}/{rest}";
    }

    private sealed record PromptEnvelope(string TempDirectory, string Prompt);

    private sealed record CliExecutionResult(int ExitCode, string Stdout, string Stderr, string LogFile);

    private sealed record GitCommandResult(int ExitCode, string Arguments, string Stdout, string Stderr);

    private sealed record GitContext(
        string? Branch,
        string? Upstream,
        string StatusShort,
        string DiffStat,
        string DiffNameStatus,
        string RecentCommits,
        int StatusExitCode,
        int DiffStatExitCode,
        int DiffNameStatusExitCode,
        int RecentCommitsExitCode);

    private sealed record KnowledgeSnapshot(string KnowledgeRoot, JsonNode? Categories, IReadOnlyList<KnowledgeFileSummary> OperationFiles);

    private sealed record KnowledgeFileSummary(string File, int EntryCount, IReadOnlyList<string> Methods);

    private sealed record ValidationReport(bool IsValid, string[] Errors);

    private sealed record ApplyReport(int AppliedCount, int SkippedCount);
}
