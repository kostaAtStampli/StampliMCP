using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class TwoScanEnforcementPrompt
{
    [McpServerPrompt(
        Name = "enforce_two_scan_extraction",
        Title = "Two-Scan Knowledge Extraction Enforcer"
    )]
    [Description(@"
Enforces two-scan pattern for knowledge extraction to prevent LLM hallucination:
1. Scan 1: Broad discovery scan with user-provided questions
2. Built-in second-scan challenge generator produces skeptical verification questions
3. Scan 2: Deep verification scan with generated challenges
4. Result: ExtractedKnowledge with verified, accurate information

This prompt ensures that all extracted knowledge is double-verified.
Scan 2 always challenges and corrects Scan 1 findings.

Usage:
- area: 'vendor_operations', 'payment_flows', 'kotlin_infrastructure', etc.
- initialQuestions: Array of discovery questions for Scan 1
- challengeAreas: Areas to challenge (validation_rules, line_numbers, constants, etc.)
")]
    public static ChatMessage[] Execute(
        [Description("Knowledge area being extracted (e.g., 'vendor_operations', 'payment_flows')")]
        string area,

        [Description("Initial discovery questions for Scan 1 (JSON array of strings)")]
        string initialQuestions,

        [Description("Areas to challenge in Scan 2 (comma-separated: validation_rules,line_numbers,constants,kotlin_files,test_coverage,operation_count)")]
        string challengeAreas
    )
    {
        Serilog.Log.Information("Prompt {Prompt} started: Enforcing two-scan extraction for {Area}",
            "enforce_two_scan_extraction", area);

        try
        {
            var scan1Questions = JsonSerializer.Deserialize<string[]>(initialQuestions)
                ?? throw new ArgumentException("Invalid initialQuestions JSON");

            var challengeAreasList = challengeAreas.Split(',').Select(a => a.Trim()).ToArray();

            var promptText = $@"
# Two-Scan Knowledge Extraction for {area}

You will perform a two-scan knowledge extraction to ensure accuracy and prevent hallucination.

## SCAN 1: Broad Discovery
First, scan the C:\STAMPLI4 codebase to answer these discovery questions:

{string.Join("\n\n", scan1Questions.Select((q, i) => $"Q{i + 1}: {q}"))}

CRITICAL RULES FOR SCAN 1:
1. Return ONLY valid JSON (no markdown, no explanations outside JSON)
2. Use Windows paths (C:\STAMPLI4\...) not WSL paths (/mnt/c/)
3. Be PRECISE with line numbers - count actual lines
4. If you find something unexpected, REPORT IT (don't hide it)
5. Structure your response as a JSON object with numbered keys:
   {{
     ""Q1_{area.ToUpper()}"": {{ ... your findings ... }},
     ""Q2_{area.ToUpper()}"": {{ ... your findings ... }},
     ...
   }}

## SCAN 2: Challenge and Verification
After Scan 1, run the built-in second-scan challenge generator with:
- scan1Results: Your Scan 1 JSON results
- challengeAreas: [{string.Join(", ", challengeAreasList.Select(a => $"\"{a}\""))}]

The tool will generate skeptical verification questions. Execute these challenges by:
1. RE-SCANNING the files mentioned in Scan 1
2. VERIFYING every claim from Scan 1
3. COUNTING actual lines, methods, constants
4. FINDING things Scan 1 may have missed
5. CORRECTING any errors from Scan 1

## FINAL OUTPUT: ExtractedKnowledge
After both scans, return an ExtractedKnowledge structure:
{{
  ""area"": ""{area}"",
  ""scan1Summary"": ""Brief summary of Scan 1 findings"",
  ""scan2Corrections"": [""List of things Scan 2 corrected from Scan 1""],
  ""requiredFields"": {{ ""field"": {{ ""type"": ""..."", ""maxLength"": ""..."" }} }},
  ""scanThese"": [
    {{ ""file"": ""C:\\STAMPLI4\\..."", ""lines"": ""X-Y"", ""purpose"": ""..."" }}
  ],
  ""helpers"": [
    {{ ""class"": ""..."", ""location"": {{ ""file"": ""..."", ""lines"": ""..."" }} }}
  ],
  ""kotlinFiles"": [
    {{ ""file"": ""..."", ""path"": ""C:\\..."", ""lines"": ""..."", ""purpose"": ""..."" }}
  ],
  ""kotlinProgress"": ""X methods migrated, Y still in Java"",
  ""scan2Challenges"": X,
  ""constants"": [
    {{ ""name"": ""..."", ""value"": ""..."", ""file"": ""..."", ""line"": X }}
  ],
  ""validationRules"": [
    {{ ""field"": ""..."", ""type"": ""required|maxLength|format"", ""value"": ""..."" }}
  ]
}}

## Challenge Areas to Focus On:
{string.Join("\n", challengeAreasList.Select(a => $"- {a}: Verify and challenge all {a.Replace('_', ' ')}"))}

## IMPORTANT:
- Trust Scan 2 results over Scan 1
- Report discrepancies between scans
- Count how many corrections Scan 2 made
- Always use Windows paths (C:\) not WSL paths (/mnt/c/)
- Return structured ExtractedKnowledge that can be parsed and saved

Begin with Scan 1 now.
";

            return new[]
            {
                new ChatMessage(ChatRole.System,
                    "You are an expert code analyzer specializing in knowledge extraction. " +
                    "You perform two-scan verification to ensure accuracy and prevent hallucination."),

                new ChatMessage(ChatRole.User, promptText)
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Prompt {Prompt} failed: {Error}", "enforce_two_scan_extraction", ex.Message);

            return new[]
            {
                new ChatMessage(ChatRole.User,
                    $"Error in two-scan enforcement prompt: {ex.Message}")
            };
        }
    }
}