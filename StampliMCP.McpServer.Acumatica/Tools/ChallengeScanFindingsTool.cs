using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ChallengeScanFindingsTool
{
    [McpServerTool(
        Name = "challenge_scan_findings",
        Title = "Scan 2 Challenge Generator",
        UseStructuredContent = true
    )]
    [Description(@"
MANDATORY: Enforces second scan by generating skeptical challenge questions based on Scan 1 results.

This tool takes Scan 1 findings and generates aggressive verification questions:
- ""Scan 1 said vendorName maxLength is 60. RE-SCAN line 119 and VERIFY this exact value.""
- ""Scan 1 found 2 validation rules. Read ENTIRE method and find ALL validations (you may have missed some).""
- ""Scan 1 claimed method is at lines 396-405. COUNT ACTUAL LINES to verify.""

Use this to prevent LLM hallucination and ensure accuracy of extracted knowledge.

Examples:
- Scan 1 finds 3 constants → Challenge: ""Find ALL constants, not just 3""
- Scan 1 finds 17 import operations → Challenge: ""Verify count by scanning driver completely""
- Scan 1 says Kotlin has 1 method → Challenge: ""Re-scan for newly added Kotlin methods""
")]
    public static ChallengeScanResult Execute(
        [Description("Scan 1 results as JSON string")]
        string scan1Results,

        [Description("Areas to challenge: validation_rules, line_numbers, constants, kotlin_files, test_coverage, operation_count")]
        string[] challengeAreas
    )
    {
        Serilog.Log.Information("Tool {Tool} started: Generating Scan 2 challenges", "challenge_scan_findings");

        try
        {
            var scan1 = JsonDocument.Parse(scan1Results);
            var challenges = new List<string>();
            var scan1Summary = new List<string>();

            // Generate skeptical questions based on Scan 1 findings
            foreach (var area in challengeAreas)
            {
                switch (area.ToLower())
                {
                    case "validation_rules":
                        if (scan1.RootElement.TryGetProperty("validationRules", out var rules))
                        {
                            var ruleCount = rules.GetArrayLength();
                            scan1Summary.Add($"Scan 1 found {ruleCount} validation rules");
                            challenges.Add($@"
CHALLENGE VALIDATION RULES:
Scan 1 claimed {ruleCount} validation rules exist.
RE-SCAN the validation method COMPLETELY - read EVERY line.
Are there MORE rules that were missed? (maxLength, format, regex, custom logic)
Return JSON with ALL validations found:
{{
  ""revalidated"": true,
  ""scan1Count"": {ruleCount},
  ""actualCount"": X,
  ""allValidations"": [
    {{""field"": ""..."", ""type"": ""required|maxLength|format"", ""value"": ""..."", ""line"": X}}
  ]
}}");
                        }
                        break;

                    case "line_numbers":
                        scan1Summary.Add("Scan 1 provided line number ranges");
                        challenges.Add(@"
VERIFY LINE NUMBERS:
Scan 1 provided line ranges (e.g., ""396-405"").
Open each file and COUNT ACTUAL LINES to verify.
Find method START (first line with method signature) and END (closing brace).
Return JSON:
{
  ""verifiedLines"": [
    {""file"": ""AcumaticaDriver.java"", ""method"": ""exportVendor"", ""claimed"": ""396-405"", ""actual"": ""557-561"", ""match"": false}
  ]
}");
                        break;

                    case "constants":
                        if (scan1.RootElement.TryGetProperty("constants", out var consts))
                        {
                            var constCount = consts.GetArrayLength();
                            scan1Summary.Add($"Scan 1 found {constCount} constants");
                            challenges.Add($@"
DEEP DIVE CONSTANTS:
Scan 1 found {constCount} constants.
CHALLENGE: Scan the ENTIRE file for ALL constants (static final, const val, public static).
Scan 1 may have only found the obvious ones.
Return JSON:
{{
  ""scan1Count"": {constCount},
  ""actualCount"": X,
  ""allConstants"": [
    {{""name"": ""..."", ""value"": ""..."", ""type"": ""static final|const val"", ""line"": X, ""file"": ""...""}}
  ]
}}");
                        }
                        break;

                    case "kotlin_files":
                        scan1Summary.Add("Scan 1 scanned Kotlin files");
                        challenges.Add(@"
KOTLIN INFRASTRUCTURE VERIFICATION:
Scan C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver COMPLETELY.
Find ALL .kt files (not just the 3 golden reference files).
Check for NEW files added since documentation was written.
Return JSON:
{
  ""basePath"": ""C:\\STAMPLI4\\core\\finsys-modern"",
  ""allKotlinFiles"": [
    {""file"": ""..."", ""path"": ""..."", ""lines"": ""1-X"", ""class"": ""..."", ""purpose"": ""...""}
  ],
  ""newFilesSinceLastUpdate"": [""...""],
  ""deletedFiles"": [""...""]
}");
                        break;

                    case "test_coverage":
                        scan1Summary.Add("Scan 1 found test files");
                        challenges.Add(@"
TEST COVERAGE DEEP DIVE:
Scan ALL test files mentioned in Scan 1.
For EACH test file, count test methods and extract scenarios tested.
Return JSON:
{
  ""testFiles"": [
    {
      ""file"": ""..."",
      ""testMethods"": [""test_createVendorSuccessfully"", ""test_idempotency"", ...],
      ""methodCount"": X,
      ""scenariosCovered"": [""happy path"", ""duplicate detection"", ""validation errors"", ...]
    }
  ]
}");
                        break;

                    case "operation_count":
                        if (scan1.RootElement.TryGetProperty("operations", out var ops))
                        {
                            var opCount = ops.GetArrayLength();
                            scan1Summary.Add($"Scan 1 found {opCount} operations");
                            challenges.Add($@"
VERIFY OPERATION COUNT:
Scan 1 claimed {opCount} operations exist.
RE-SCAN AcumaticaDriver.java COMPLETELY - count ALL public methods that match IDualFinsysDriver interface.
Return JSON:
{{
  ""scan1Count"": {opCount},
  ""actualCount"": X,
  ""allOperations"": [
    {{""method"": ""..."", ""category"": ""vendors|payments|etc"", ""lineStart"": X, ""lineEnd"": X}}
  ],
  ""missedOperations"": [""...""]
}}");
                        }
                        break;

                    default:
                        Serilog.Log.Warning("Unknown challenge area: {Area}", area);
                        break;
                }
            }

            var result = new ChallengeScanResult
            {
                Scan1Summary = string.Join("; ", scan1Summary),
                ChallengeQuestions = challenges.ToArray(),
                ChallengeCount = challenges.Count,
                Instruction = @"
SCAN 2 WORKFLOW:
1. Ask Claude CLI these challenge questions (use structured prompts)
2. Compare Scan 2 results to Scan 1 results
3. Count discrepancies (where Scan 2 differs from Scan 1)
4. Trust Scan 2 results over Scan 1 (Scan 2 is deeper + verified)
5. Update Knowledge/*.json files with Scan 2 findings
"
            };

            Serilog.Log.Information("Tool {Tool} completed: Generated {Count} challenges", "challenge_scan_findings", challenges.Count);
            return result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Tool {Tool} failed: {Error}", "challenge_scan_findings", ex.Message);
            return new ChallengeScanResult
            {
                Scan1Summary = "Error parsing Scan 1 results",
                ChallengeQuestions = new[] { "Unable to generate challenges - check Scan 1 JSON format" },
                ChallengeCount = 0,
                Instruction = $"Error: {ex.Message}"
            };
        }
    }
}
