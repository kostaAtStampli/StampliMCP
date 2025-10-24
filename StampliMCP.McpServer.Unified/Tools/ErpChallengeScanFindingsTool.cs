using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class ErpChallengeScanFindingsTool
{
    [McpServerTool(
        Name = "erp__challenge_scan_findings",
        Title = "Two-Scan Challenge Generator (ERP-agnostic)",
        UseStructuredContent = true
    )]
    [Description("Generate skeptical verification questions from Scan 1 results to enforce a second, deeper scan.")]
    public static ChallengeScanResult Execute(
        [Description("Scan 1 results as JSON string")] string scan1Results,
        [Description("Areas to challenge: validation_rules, line_numbers, constants, kotlin_files, test_coverage, operation_count")] string[] challengeAreas
    )
    {
        Serilog.Log.Information("Tool {Tool} started: ERP-agnostic Scan 2 challenges", "erp__challenge_scan_findings");

        try
        {
            using var scan1 = JsonDocument.Parse(scan1Results);
            var challenges = new List<string>();
            var summary = new List<string>();

            foreach (var area in challengeAreas)
            {
                switch (area.Trim().ToLowerInvariant())
                {
                    case "validation_rules":
                        summary.Add("Scan 1 provided validation rules");
                        challenges.Add(@"VALIDATION RULES VERIFICATION:
Re-scan the validation logic completely.
List ALL validation rules (required, maxLength, formats, business logic).
Return JSON with counts and enumerated rules including file+line evidence.");
                        break;

                    case "line_numbers":
                        summary.Add("Scan 1 included line ranges");
                        challenges.Add(@"LINE NUMBER VERIFICATION:
Open each referenced file and count ACTUAL lines for methods/blocks claimed.
Return verified start/end lines and whether they match Scan 1.");
                        break;

                    case "constants":
                        summary.Add("Scan 1 listed constants");
                        challenges.Add(@"CONSTANTS DEEP DIVE:
Scan entire files for ALL constants (const/static final/etc.).
Return JSON: total, full list with names, values, and file+line.");
                        break;

                    case "operation_count":
                        summary.Add("Scan 1 counted operations");
                        challenges.Add(@"OPERATION INVENTORY VERIFICATION:
Re-scan driver/service classes completely and count ALL public operations.
Return JSON: total and list with method signatures and line ranges.");
                        break;

                    case "kotlin_files":
                        summary.Add("Scan 1 referenced Kotlin files");
                        challenges.Add(@"KOTLIN FILES INVENTORY:
Enumerate ALL Kotlin files relevant to this feature area.
Return JSON with files, purpose, and line counts.");
                        break;

                    case "test_coverage":
                        summary.Add("Scan 1 referenced tests");
                        challenges.Add(@"TEST COVERAGE PASS:
List test files and enumerate test methods/scenarios covered.
Return JSON with method count and scenario summaries.");
                        break;

                    default:
                        Serilog.Log.Warning("Unknown challenge area: {Area}", area);
                        break;
                }
            }

            return new ChallengeScanResult
            {
                Scan1Summary = string.Join("; ", summary),
                ChallengeQuestions = challenges.ToArray(),
                ChallengeCount = challenges.Count,
                Instruction = @"SCAN 2 WORKFLOW:
1) Ask these questions systematically
2) Re-scan files and verify every claim
3) Prefer Scan 2 where it corrects Scan 1
4) Update knowledge using verified evidence"
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "erp__challenge_scan_findings", ex.Message);
            return new ChallengeScanResult
            {
                Scan1Summary = "Error parsing Scan 1 results",
                ChallengeQuestions = new[] { "Invalid Scan 1 JSON" },
                ChallengeCount = 0,
                Instruction = ex.Message
            };
        }
    }
}

public sealed class ChallengeScanResult
{
    public string? Scan1Summary { get; set; }
    public string[] ChallengeQuestions { get; set; } = Array.Empty<string>();
    public int ChallengeCount { get; set; }
    public string? Instruction { get; set; }
}
