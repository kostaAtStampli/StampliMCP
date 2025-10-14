namespace StampliMCP.McpServer.Acumatica.Tests.LiveLLM;

/// <summary>
/// Standardized prompt templates for LiveLLM tests
/// Based on successful patterns from minimal tests
/// </summary>
public static class PromptTemplates
{
    /// <summary>
    /// Autonomous workflow prompt - 2025 AI-first approach with FORMAT ENFORCEMENT
    /// Uses MCP prompt (not tool) to enforce ═══ FILES SCANNED ═══ format
    /// </summary>
    public static string AutonomousWorkflow(string feature) =>
        $"Use the kotlin_tdd_tasklist MCP prompt with feature: '{feature}'. " +
        "The prompt will guide you to call kotlin_tdd_workflow tool, scan files, and output a formatted tasklist. " +
        "Follow the format requirements exactly: start with ═══ FILES SCANNED ═══, include Constants/Methods/Patterns proof, then TDD steps.";

    /// <summary>
    /// Explicit TDD workflow prompt - numbered steps ensure Claude follows exact sequence
    /// </summary>
    public static string TddWorkflow(string operation, string testFilePath, string implFilePath) => $@"You have access to the kotlin_tdd_workflow MCP tool.

Task: Implement {operation} operation in Kotlin using TDD methodology.

Steps:
1. Use kotlin_tdd_workflow tool with command='start' and context='{operation}'
2. Read the knowledge returned (test templates, validation rules, etc.)
3. Write a simple test file to: {testFilePath}
4. Write a simple implementation to: {implFilePath}
5. Keep it minimal - just basic structure is fine for this test

Write all files to the current working directory.";
}
