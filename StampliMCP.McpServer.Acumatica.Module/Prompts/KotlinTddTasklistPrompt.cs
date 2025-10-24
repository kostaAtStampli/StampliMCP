using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class KotlinTddTasklistPrompt
{
    [McpServerPrompt(Name = "kotlin_tdd_tasklist")]
    [Description("Enforces strict format for Kotlin TDD implementation tasklist. Returns conversation that primes AI to output ═══ FILES SCANNED ═══ format with Constants/Methods/Patterns proof, followed by RED→GREEN→REFACTOR steps. Use this instead of calling kotlin_tdd_workflow tool directly when you need format compliance.")]
    public static ChatMessage[] CreateTasklist(
        [Description("Feature description in natural language (e.g., 'vendor custom field import from Acumatica', 'bill payment export with validation')")]
        string feature)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                """
                You are a Kotlin TDD expert implementing Acumatica ERP integrations.

                CRITICAL: You MUST output in this EXACT format with NO preamble, NO summary before it:

                ═══ FILES SCANNED ═══
                1. /mnt/c/STAMPLI4/path/to/File.java:line-range
                   Constants: CONSTANT_1=value, CONSTANT_2=value  |  Methods: methodName(params): ReturnType  |  Patterns: Pattern description
                2. /mnt/c/STAMPLI4/path/to/NextFile.java:line-range
                   Constants: ...  |  Methods: ...  |  Patterns: ...
                [Repeat for ALL files in mandatoryFileScanning]
                ═══════════════════

                ## TDD Implementation Steps

                ### Phase 1: RED (Tests First)
                1. [RED] Write test for...
                2. [RED] Write test for...
                3. [RED] Verify ALL tests FAIL

                ### Phase 2: GREEN (Implementation)
                4. [GREEN] Implement...
                5. [GREEN] Verify tests PASS

                ### Phase 3: REFACTOR
                6. [REFACTOR] Extract common patterns...
                7. [REFACTOR] Add documentation...

                ENFORCEMENT RULES:
                - START your response with "═══ FILES SCANNED ═══"
                - NO "Summary", NO "Analysis", NO "I have analyzed"
                - List ALL critical files from mandatoryFileScanning
                - Prove you scanned files with Constants/Methods/Patterns from each
                - Include specific line numbers you referenced
                - 10-20 TDD steps in RED→GREEN→REFACTOR phases

                WORKFLOW:
                1. Call kotlin_tdd_workflow tool (command: start, context: <feature>)
                2. Use Read tool on ALL files in mandatoryFileScanning response
                3. Extract constants, methods, patterns from each file
                4. Output formatted tasklist starting with ═══ FILES SCANNED ═══
                """),

            new ChatMessage(ChatRole.User, $"""
                Create TDD implementation tasklist for: {feature}

                Requirements:
                - Use kotlin_tdd_workflow tool to get flow guidance and file list
                - Scan ALL files in mandatoryFileScanning using Read tool
                - Output in ═══ FILES SCANNED ═══ format (NO preamble)
                - Include Constants/Methods/Patterns proof from each file
                - Create 10-20 TDD steps (RED → GREEN → REFACTOR)

                Start immediately with workflow execution.
                """),

            new ChatMessage(ChatRole.Assistant, $"""
                Understood. I'll create a TDD tasklist for "{feature}" in the required format.

                Step 1: Calling kotlin_tdd_workflow tool to get flow guidance and mandatory files...

                [Tool call: kotlin_tdd_workflow with command="start", context="{feature}"]

                Step 2: I will use Read tool on ALL files returned in mandatoryFileScanning

                Step 3: I will output the tasklist starting with:

                ═══ FILES SCANNED ═══
                [with proof: Constants, Methods, Patterns from each file]
                ═══════════════════

                ## TDD Implementation Steps
                [RED → GREEN → REFACTOR phases]

                Proceeding with tool call now...
                """)
        };
    }
}
