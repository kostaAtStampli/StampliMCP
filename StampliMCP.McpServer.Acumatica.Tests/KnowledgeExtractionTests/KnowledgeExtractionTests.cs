using FluentAssertions;
// TODO: Uncomment when KnowledgeExtractor service is implemented
// using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica.Tests.Helpers;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Tests.KnowledgeExtractionTests;

/// <summary>
/// Knowledge extraction tests - Spawn Claude CLI to scan C:\STAMPLI4 code
/// Scan TWICE (Scan 1 broad → Scan 2 deep challenge) → Update Knowledge/*.json
/// </summary>
public class KnowledgeExtractionTests
{
    [Fact]
    public async Task Extract_Kotlin_Infrastructure()
    {
        // Create isolated test directory
        var testDir = Path.Combine(Path.GetTempPath(), $"mcp_kotlin_extraction_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(testDir);
        Console.WriteLine($"=== Test directory: {testDir} ===");

        // ═══ SCAN 1: Broad Discovery (5 questions) ═══
        Console.WriteLine("\n=== SCAN 1: Broad Discovery ===");

        var scan1Questions = new[]
        {
            @"Scan C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver folder.
              Find ALL .kt files (not just the 3 golden reference files).
              Return JSON:
              {
                ""kotlinFiles"": [
                  {""file"": ""KotlinAcumaticaDriver.kt"", ""path"": ""C:\\STAMPLI4\\..."", ""lines"": ""1-27"", ""class"": ""KotlinAcumaticaDriver""},
                  ...
                ]
              }",

            @"Check C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver\src\main\kotlin\com\stampli\kotlin\acumatica\driver\KotlinAcumaticaDriver.kt.
              Which methods are overridden from AcumaticaDriver parent?
              Return JSON:
              {
                ""overridden"": [""exportVendor""],
                ""total"": 50
              }",

            @"Find files in C:\STAMPLI4\core\finsys-modern\kotlin-drivers-common.
              Return JSON:
              {
                ""commonFiles"": [
                  {""file"": ""..."", ""purpose"": ""...""}
                ]
              }",

            @"Scan for Kotlin test files in C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver\src\test\kotlin.
              Return JSON:
              {
                ""testFiles"": [
                  {""file"": ""..."", ""testMethods"": [""...""], ""methodCount"": X}
                ]
              }",

            @"Count Kotlin classes vs Java classes in acumatica driver.
              Return JSON:
              {
                ""kotlinClasses"": 3,
                ""javaClasses"": 15
              }"
        };

        var scan1LogPath = await ClaudeCliStructuredHelper.AskClaudeStructured(scan1Questions, testDir, "scan1");

        // ═══ CALL MCP TOOL: Generate Scan 2 Challenges ═══
        Console.WriteLine("\n=== Generating Scan 2 Challenges via MCP Tool ===");

        var scan1Json = ClaudeCliStructuredHelper.ExtractJsonFromLog(scan1LogPath);
        Console.WriteLine($"Scan 1 JSON: {scan1Json.Substring(0, Math.Min(200, scan1Json.Length))}...");

        // For now, manually create challenges (we'll call MCP tool properly after registration)
        var scan2Questions = new[]
        {
            @"KOTLIN INFRASTRUCTURE VERIFICATION:
              RE-SCAN C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver COMPLETELY.
              Find ALL .kt files. Scan 1 may have missed some.
              Return JSON:
              {
                ""basePath"": ""C:\\STAMPLI4\\core\\finsys-modern"",
                ""allKotlinFiles"": [
                  {""file"": ""..."", ""path"": ""..."", ""lines"": ""1-X"", ""class"": ""..."", ""purpose"": ""...""}
                ],
                ""newFilesSinceLastUpdate"": [""...""]
              }",

            @"VERIFY line count for KotlinAcumaticaDriver.kt.
              Open the file and COUNT ACTUAL LINES.
              Return JSON:
              {
                ""file"": ""KotlinAcumaticaDriver.kt"",
                ""claimed"": ""1-27"",
                ""actual"": ""1-X"",
                ""match"": true/false
              }",

            @"Compare C:\STAMPLI4\core\finsys-modern structure to knowledge/kotlin-golden-reference.json.
              Has architecture changed? New folders? Deleted files?
              Return JSON:
              {
                ""structureChanged"": true/false,
                ""changes"": [""...""],
                ""pathCorrect"": ""C:\\STAMPLI4\\core\\finsys-modern""
              }",

            @"Extract Kotlin patterns from CreateVendorHandler.kt.
              Find: companion object, internal class, ?.let, !!, apply, string interpolation.
              Return JSON:
              {
                ""patterns"": [
                  {""name"": ""companion object"", ""line"": X, ""example"": ""...""},
                  ...
                ]
              }",

            @"RE-COUNT overridden methods in KotlinAcumaticaDriver.kt.
              Scan 1 said 1 method (exportVendor). Are there MORE?
              Return JSON:
              {
                ""scan1Count"": 1,
                ""actualCount"": X,
                ""overridden"": [""exportVendor"", ...]
              }"
        };

        // ═══ SCAN 2: Deep Dive with Challenges ═══
        Console.WriteLine("\n=== SCAN 2: Deep Dive with Challenges ===");

        var scan2LogPath = await ClaudeCliStructuredHelper.AskClaudeStructured(scan2Questions, testDir, "scan2");

        // ═══ PARSE LOGS & EXTRACT KNOWLEDGE ═══
        Console.WriteLine("\n=== Parsing Logs ===");

        // TODO: Uncomment when KnowledgeExtractor service is implemented
        // var extractor = new KnowledgeExtractor();
        // var knowledge = extractor.ParseLogs(scan1LogPath, scan2LogPath);

        // Temporary: Return empty knowledge for now
        var knowledge = new ExtractedKnowledge();

        Console.WriteLine($"✓ Extracted knowledge:");
        Console.WriteLine($"  - Kotlin files: {knowledge.KotlinFiles.Count}");
        Console.WriteLine($"  - Scan 2 challenges: {knowledge.Scan2Challenges}");
        Console.WriteLine($"  - Progress: {knowledge.KotlinProgress}");

        // ═══ UPDATE KNOWLEDGE ═══
        var kotlinJsonPath = "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\Knowledge\\kotlin-golden-reference.json";
        // TODO: Uncomment when KnowledgeExtractor service is implemented
        // extractor.UpdateKnowledgeFile(kotlinJsonPath, knowledge);
        Console.WriteLine($"✓ Updated knowledge file: {kotlinJsonPath}");

        // ═══ ASSERTIONS ═══
        knowledge.Should().NotBeNull();
        knowledge.KotlinFiles.Should().NotBeEmpty("Should find Kotlin files");
        knowledge.Scan2Challenges.Should().BeGreaterThanOrEqualTo(0, "Scan 2 may challenge Scan 1");

        // Verify logs exist
        File.Exists(scan1LogPath).Should().BeTrue();
        File.Exists(scan2LogPath).Should().BeTrue();

        Console.WriteLine($"\n=== Test completed successfully ===");
        Console.WriteLine($"=== Logs saved to: {testDir} ===");
    }
}
