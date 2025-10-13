# Testing MCP Nuclear 2025 with Live LLMs

## Overview

This guide covers testing the MCP Nuclear 2025 server with real LLMs (Claude Code CLI) to validate the complete workflow from user prompt to code generation.

## Test Infrastructure

### Components

**SandboxManager.cs** - Manages temporary test directories
- Creates isolated Kotlin project structure per test
- Auto-cleanup after tests complete
- Located in: `StampliMCP.McpServer.Acumatica.Tests/LiveLLM/`

**ConversationLogger.cs** - Logs LLM↔MCP interactions
- Tracks all conversation turns
- Records tool calls and responses
- Saves successful flows to `GoldenFlows/` directory
- Generates JSON logs for analysis

**ClaudeCodeClient.cs** - Wrapper for `claude` CLI
- Sends prompts to Claude Code CLI
- Captures responses
- Integrates with ConversationLogger
- Handles timeouts and errors

**TestMetrics.cs** - Tracks performance metrics
- Duration, token usage, cost estimation
- Aggregates metrics across multiple tests
- Generates comparison reports

**FullWorkflowTests.cs** - Live LLM integration tests
- Marked with `[Trait("Category", "LiveLLM")]`
- **Skipped by default** (expensive, slow)
- Tests complete workflow from user prompt to code

## Prerequisites

### 1. Claude Code CLI

Verify Claude CLI is installed and accessible:

```bash
claude --version
```

Expected output:
```
Claude CLI 2.0.14 (or later)
```

If not installed, install from: https://github.com/anthropics/claude-cli

### 2. MCP Server Configuration

Ensure the MCP server is configured in Claude Code. The test infrastructure creates this automatically in `.claude-code-config.json`:

```json
{
  "mcpServers": {
    "stampli_acumatica": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj"
      ],
      "env": {
        "MCP_DEBUG": "false"
      }
    }
  }
}
```

### 3. .NET SDK

Ensure .NET 10.0 SDK is installed:

```bash
dotnet --version
```

## Running Live LLM Tests

### Run All Live LLM Tests

```bash
cd /mnt/c/Users/Kosta/source/repos/StampliMCP
dotnet test --filter "Category=LiveLLM" --logger "console;verbosity=detailed"
```

**Warning**: These tests:
- Take 2-5 minutes each
- Cost $0.10-$0.50 per test (Claude API usage)
- Require Claude CLI authentication

### Run Specific Test

```bash
dotnet test --filter "FullyQualifiedName~FullWorkflowTests.LLM_Should_Implement_Vendor_Export_Feature"
```

### View Test Output

Tests generate output in:
- **Console**: Real-time progress and results
- **GoldenFlows/**: Successful conversation flows (JSON)
- **TestResults/**: xUnit test results

## Test Scenarios

### 1. Vendor Export Feature

**Test**: `LLM_Should_Implement_Vendor_Export_Feature`

**User Prompt**:
```
"Implement exportVendor operation using TDD. The operation should:
1. Accept vendor data (vendorName, vendorId, address)
2. Validate required fields
3. Call Acumatica API
4. Return success/failure response

Follow the MCP guidance for implementation patterns."
```

**Expected Flow**:
1. LLM calls `kotlin_tdd_workflow(command="start")`
2. LLM receives complete knowledge (templates, validations, file pointers)
3. LLM writes test file in sandbox using provided template
4. LLM calls `kotlin_tdd_workflow(command="continue", context="tests failing")`
5. LLM writes implementation file using provided template
6. LLM verifies tests pass
7. Test validates generated files exist and contain expected patterns

**Success Criteria**:
- Test file created with proper structure
- Implementation file created with proper structure
- Files contain validation logic
- Files contain Acumatica API calls
- Less than 10 LLM turns required
- Less than $0.50 cost

### 2. Bill Payment Processing

**Test**: `LLM_Should_Implement_Bill_Payment_Feature`

**User Prompt**:
```
"Implement exportBillPayment operation using TDD. Follow MCP guidance."
```

**Expected Flow**:
Similar to vendor export but with payment-specific validations.

**Success Criteria**:
- Payment validation logic present
- Amount validation implemented
- Vendor ID validation implemented
- Less than 8 LLM turns

### 3. Error Recovery

**Test**: `LLM_Should_Recover_From_401_Error`

**User Prompt**:
```
"Implement vendor export. I'm getting 401 unauthorized errors when testing."
```

**Expected Flow**:
1. LLM calls `kotlin_tdd_workflow(command="start")`
2. LLM implements feature
3. LLM encounters 401 error (simulated)
4. LLM calls `kotlin_tdd_workflow(command="query", context="401 unauthorized")`
5. LLM receives authentication guidance
6. LLM fixes authentication in code

**Success Criteria**:
- LLM uses query command for help
- LLM adds authentication code
- Final implementation includes auth

## Analyzing Test Results

### Conversation Logs

After successful tests, review conversation logs:

```bash
ls GoldenFlows/
# Example: vendor_export_20250113_143022.json
```

**Log Format**:
```json
{
  "testName": "LLM_Should_Implement_Vendor_Export_Feature",
  "timestamp": "2025-01-13T14:30:22Z",
  "totalTurns": 8,
  "turns": [
    {
      "turnNumber": 1,
      "timestamp": "2025-01-13T14:30:22Z",
      "actor": "LLM",
      "content": {
        "prompt": "Implement exportVendor...",
        "response": "I'll help implement...",
        "toolCalls": [
          {
            "toolName": "kotlin_tdd_workflow",
            "arguments": { "command": "start", "context": "exportVendor" },
            "result": { "sessionId": "tdd_001", ... }
          }
        ]
      }
    },
    ...
  ]
}
```

### Metrics Reports

Generate metrics report:

```bash
dotnet test --filter "Category=LiveLLM" --logger "console;verbosity=minimal" > test_results.txt
```

**Metrics Tracked**:
- **Duration**: Total time from prompt to completion
- **Turns**: Number of LLM↔MCP round trips
- **Tool Calls**: How many MCP tool calls made
- **Files Created**: Number of files written
- **Tokens**: Input/output token usage
- **Cost**: Estimated cost (based on Claude pricing)

**Example Report**:
```
=== LLM Test Metrics Report ===
Generated: 2025-01-13 14:35:00 UTC

Overall:
  Total Tests: 3
  Successful: 3 (100.0%)
  Failed: 0
  Avg Duration: 127.3s
  Avg Turns: 8.7
  Total Cost: $1.23

By LLM:
  claude-3-5-sonnet:
    Success Rate: 3/3 (100.0%)
    Avg Duration: 127.3s
    Avg Cost: $0.4100
```

## Troubleshooting

### Test Fails: "Claude CLI not found"

**Solution**:
```bash
# Verify claude CLI is in PATH
which claude

# If not found, add to PATH or install
npm install -g @anthropic/claude-cli
```

### Test Fails: "MCP server not responding"

**Solution**:
```bash
# Test MCP server manually
cd StampliMCP.McpServer.Acumatica
dotnet run

# In another terminal
echo '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2024-11-05"},"id":1}' | dotnet run
```

### Test Times Out

**Solution**:
- Increase timeout in test (default: 5 minutes)
- Check Claude API rate limits
- Verify network connectivity

### Test Fails: "Files not created"

**Solution**:
- Check sandbox directory permissions
- Review conversation log to see what LLM attempted
- Verify LLM received complete knowledge from MCP

### High Cost per Test

**Expected Costs**:
- Simple features: $0.10-$0.20
- Complex features: $0.30-$0.50
- Error recovery: $0.15-$0.30

**Cost Reduction**:
- Use `analysis_depth="quick"` for simple tests
- Cache successful flows and compare against them
- Run tests selectively, not full suite

## Best Practices

### 1. Run Tests Selectively

Don't run full LiveLLM suite on every commit:

```bash
# Regular CI: Run integration tests only (fast, free)
dotnet test --filter "Category=Integration"

# Pre-release: Run LiveLLM tests (slow, costly)
dotnet test --filter "Category=LiveLLM"
```

### 2. Review Golden Flows

After successful tests, save conversation logs as golden patterns:

```bash
cp GoldenFlows/vendor_export_20250113_143022.json GoldenFlows/vendor_export_golden.json
```

Use these for:
- Regression detection (compare new flows to golden)
- Documentation (show ideal LLM behavior)
- Debugging (compare failed flow to successful flow)

### 3. Monitor Metrics

Track metrics over time to detect regressions:

| Version | Avg Turns | Avg Duration | Avg Cost | Success Rate |
|---------|-----------|--------------|----------|--------------|
| v2.0.0 | 15.2 | 245s | $0.95 | 67% |
| v3.0.0 | 8.7 | 127s | $0.41 | 100% |

**Regression Indicators**:
- Avg Turns > 12 (too many round trips)
- Avg Cost > $0.60 (inefficient token usage)
- Success Rate < 90% (MCP not providing clear guidance)

### 4. Test Pyramid

Balance test types:

```
        LiveLLM (3 tests)         ← Expensive, slow, realistic
       /                  \
      /  Integration (15)  \      ← Moderate, fast, validated
     /                      \
    /     Unit (150+)        \    ← Cheap, instant, isolated
   /__________________________\
```

### 5. CI/CD Integration

**Local Development**:
```bash
# Fast feedback loop
dotnet test --filter "Category!=LiveLLM"
```

**Pull Request CI**:
```bash
# Integration tests only
dotnet test --filter "Category=Integration"
```

**Nightly Build**:
```bash
# Full suite including LiveLLM
dotnet test --filter "Category=LiveLLM" --logger "trx;LogFileName=live_llm_results.trx"
```

**Release Candidate**:
```bash
# Full suite + metrics report
dotnet test --filter "Category=LiveLLM"
# Generate metrics report
# Compare against previous release metrics
```

## Adding New Live LLM Tests

### 1. Create Test Method

```csharp
[Fact]
[Trait("Category", "LiveLLM")]
public async Task LLM_Should_Implement_Your_Feature()
{
    // Arrange
    var sandbox = _sandboxManager.CreateSandbox("your_feature");
    var logger = new ConversationLogger("your_feature");
    var client = new ClaudeCodeClient(sandbox, logger);

    await client.StartAsync();

    var startTime = DateTime.UtcNow;

    try
    {
        // Act
        var response = await client.SendPromptAsync(
            "Implement yourFeature operation using TDD. Follow MCP guidance.",
            timeout: TimeSpan.FromMinutes(5)
        );

        // Assert
        var files = client.GetCreatedFiles();
        files.Should().Contain(f => f.EndsWith("YourFeatureTest.kt"));
        files.Should().Contain(f => f.EndsWith("YourFeature.kt"));

        var testFile = File.ReadAllText(files.First(f => f.EndsWith("Test.kt")));
        testFile.Should().Contain("@Test");
        testFile.Should().Contain("yourFeature");

        // Success - save golden flow
        logger.SaveSuccessfulFlow(sandbox);

        // Track metrics
        var metrics = new TestMetrics
        {
            TestName = "LLM_Should_Implement_Your_Feature",
            LLMType = "claude-3-5-sonnet",
            Success = true,
            StartTime = startTime,
            EndTime = DateTime.UtcNow,
            TotalTurns = logger.GetTurnCount(),
            ToolCalls = logger.GetToolCallCount(),
            FilesCreated = files
        };

        Console.WriteLine(metrics.ToSummary());
    }
    catch (Exception ex)
    {
        // Failure - save conversation for debugging
        logger.SaveConversation(sandbox);
        throw;
    }
}
```

### 2. Define Success Criteria

Document expected behavior:
- What files should be created?
- What code patterns should be present?
- How many LLM turns expected?
- What's the cost budget?

### 3. Add to CI/CD

Update GitHub Actions workflow (see next section).

## GitHub Actions Integration

See `.github/workflows/mcp-live-tests.yml` for CI/CD configuration.

## Cost Estimation

**Claude 3.5 Sonnet Pricing** (as of 2025):
- Input: $3.00 per 1M tokens
- Output: $15.00 per 1M tokens

**Typical Test Token Usage**:
- Simple feature: 10k input + 5k output = $0.11
- Complex feature: 25k input + 15k output = $0.30
- Full test suite (3 tests): ~$0.60-$1.50

**Annual Cost Estimate**:
- Daily nightly runs: $1.00 × 365 = $365/year
- Per-PR runs (integration only): $0/year
- Release testing (monthly): $1.50 × 12 = $18/year

**Total**: ~$385/year for comprehensive LLM testing

## FAQ

### Q: Why skip LiveLLM tests by default?

**A**: They're expensive (real API costs) and slow (2-5 min each). Run them:
- Before releases
- After major MCP changes
- In nightly builds
- Not on every commit

### Q: Can I use GPT-4 instead of Claude?

**A**: Yes, implement a new `GPT4Client.cs` similar to `ClaudeCodeClient.cs`. Update metrics to use GPT-4 pricing.

### Q: How do I debug a failing LiveLLM test?

**A**:
1. Check conversation log in `GoldenFlows/`
2. Review what tool calls LLM made
3. Compare to successful golden flow
4. Check if MCP returned complete knowledge
5. Verify sandbox files were created

### Q: Can I run LiveLLM tests without API costs?

**A**: Not really - they're designed to test with real LLMs. For free testing, use integration tests with `McpTestClient` (mocked MCP interactions).

---

**Last Updated**: January 13, 2025
**Version**: 3.0.0
