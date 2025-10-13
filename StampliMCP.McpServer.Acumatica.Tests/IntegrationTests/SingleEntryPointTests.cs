using System.Text.Json.Nodes;
using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.IntegrationTests;

/// <summary>
/// Integration tests for single entry point architecture (v3.0.0)
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Integration")]
public sealed class SingleEntryPointTests : IDisposable
{
    private readonly McpTestClient _client;

    public SingleEntryPointTests()
    {
        _client = new McpTestClient(captureStderr: true, debugMode: false);
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
        tools.Count.Should().Be(2, "Should only expose kotlin_tdd_workflow and health_check");

        var toolNames = tools.Select(t => t?["name"]?.GetValue<string>()).ToList();
        toolNames.Should().Contain("kotlin_tdd_workflow");
        toolNames.Should().Contain("health_check");

        Console.WriteLine($"✓ Verified 2 tools: {string.Join(", ", toolNames)}");
    }

    [Fact]
    public async Task Workflow_Start_Should_Return_Complete_Knowledge_Upfront()
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
        result["sessionId"].Should().NotBeNull();
        result["phase"]!.GetValue<string>().Should().Be("RED");

        // Verify ALL knowledge provided upfront
        var knowledge = result["knowledge"];
        knowledge.Should().NotBeNull();
        knowledge!["testCode"].Should().NotBeNull();
        knowledge["implementationCode"].Should().NotBeNull();
        knowledge["validationRules"].Should().NotBeNull();
        knowledge["errorMessages"].Should().NotBeNull();
        knowledge["legacyFiles"].Should().NotBeNull();
        knowledge["requiredFields"].Should().NotBeNull();

        var tasklist = result["tasklist"] as JsonArray;
        tasklist.Should().NotBeNull();
        tasklist!.Count.Should().BeGreaterThan(5, "Should have multiple TDD steps");

        Console.WriteLine($"✓ Session: {result["sessionId"]}");
        Console.WriteLine($"✓ Phase: {result["phase"]}");
        Console.WriteLine($"✓ Tasks: {tasklist.Count}");
    }

    [Fact]
    public async Task Workflow_Should_Track_Session_Through_Phases()
    {
        // Arrange
        await _client.Initialize();

        // Start workflow
        var startResult = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "start",
            context = "export bill payment",
            sessionId = (string?)null
        });

        var sessionId = startResult["sessionId"]!.GetValue<string>();
        sessionId.Should().NotBeNullOrEmpty();

        // Continue with wrong phase (should fail)
        var wrongPhase = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "continue",
            context = "tests are passing", // Wrong! Should fail first
            sessionId = sessionId
        });

        wrongPhase["error"].Should().NotBeNull("Should reject skipping RED phase");
        wrongPhase["error"]!.ToString().Should().Contain("must fail first");

        // Continue with correct phase
        var correctPhase = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "continue",
            context = "tests are failing as expected",
            sessionId = sessionId
        });

        correctPhase["phase"]!.GetValue<string>().Should().Be("GREEN");
        correctPhase["sessionId"]!.GetValue<string>().Should().Be(sessionId);

        Console.WriteLine("✓ Session state tracked correctly");
        Console.WriteLine($"✓ Enforced TDD RED→GREEN progression");
    }

    [Fact]
    public async Task Workflow_Query_Should_Provide_Help()
    {
        // Arrange
        await _client.Initialize();

        // Act - Ask for auth help
        var authHelp = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "query",
            context = "getting 401 unauthorized error",
            sessionId = (string?)null
        });

        // Assert
        authHelp.Should().NotBeNull();
        authHelp["problem"].Should().NotBeNull();
        authHelp["solution"].Should().NotBeNull();
        authHelp["solution"]!.ToString().Should().Contain("AcumaticaAuthenticator");

        Console.WriteLine("✓ Help system working");
    }

    [Fact]
    public async Task Workflow_List_Should_Show_All_Operations()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "list",
            context = "",
            sessionId = (string?)null
        });

        // Assert
        result.Should().NotBeNull();
        result["totalCategories"]!.GetValue<int>().Should().BeGreaterThan(5);
        result["totalOperations"]!.GetValue<int>().Should().BeGreaterThan(40);

        var categories = result["categories"] as JsonArray;
        categories.Should().NotBeNull();

        Console.WriteLine($"✓ Categories: {result["totalCategories"]}");
        Console.WriteLine($"✓ Operations: {result["totalOperations"]}");
    }

    [Fact]
    public async Task Workflow_Should_Handle_Unknown_Feature_Gracefully()
    {
        // Arrange
        await _client.Initialize();

        // Act
        var result = await _client.CallTool("kotlin_tdd_workflow", new
        {
            command = "start",
            context = "implement blockchain integration",
            sessionId = (string?)null
        });

        // Assert
        result["needsClarification"]?.GetValue<bool>().Should().BeTrue();
        result["availableCategories"].Should().NotBeNull();
        result["examples"].Should().NotBeNull();

        Console.WriteLine("✓ Unknown feature handled with clarification");
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
