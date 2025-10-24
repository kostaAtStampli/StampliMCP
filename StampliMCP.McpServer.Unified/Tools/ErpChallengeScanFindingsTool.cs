using System;
using System.Collections.Generic;
using System.Text.Json;

namespace StampliMCP.McpServer.Unified.Tools;

internal static class ChallengeScanGenerator
{
    internal static ChallengeScanResult Generate(string scan1Results, IReadOnlyCollection<string> challengeAreas)
    {
        Serilog.Log.Information("Challenge scan generation started");

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

            return new ChallengeScanResult(
                string.Join("; ", summary),
                challenges.ToArray(),
                challenges.Count,
                @"SCAN 2 WORKFLOW:
1) Ask these questions systematically
2) Re-scan files and verify every claim
3) Prefer Scan 2 where it corrects Scan 1
4) Update knowledge using verified evidence");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Challenge scan generation failed: {Error}", ex.Message);
            return new ChallengeScanResult(
                "Error parsing Scan 1 results",
                new[] { "Invalid Scan 1 JSON" },
                0,
                ex.Message);
        }
    }
}

internal sealed record ChallengeScanResult(
    string? Scan1Summary,
    IReadOnlyList<string> ChallengeQuestions,
    int ChallengeCount,
    string? Instruction);
