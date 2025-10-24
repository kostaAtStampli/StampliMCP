using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class TestPlanningPrompt
{
    [McpServerPrompt(Name = "plan_comprehensive_tests")]
    [Description("QA engineer conversation for test planning. AI generates comprehensive test scenarios: happy path, edge cases, error cases, performance, security. More engaging than tool - feels like collaborative test design session.")]
    public static ChatMessage[] PlanComprehensiveTests(
        [Description("Operation name to generate tests for (e.g., 'exportVendor', 'getVendors', 'exportBillPayment')")]
        string operationName)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                """
                You are a senior QA engineer specializing in ERP integration testing.
                You excel at designing comprehensive test plans that cover happy paths, edge cases, error scenarios, performance, and security.
                You think like an attacker, finding edge cases that developers might miss.
                You create actionable test scenarios with clear setup, execution, and validation steps.
                """),

            new ChatMessage(ChatRole.User, $"""
                I need a comprehensive test plan for this operation:

                **Operation**: {operationName}

                **Available MCP Tools** (unified):
                - `erp__query_knowledge(erp, query, scope?)` – Operation specs, validation rules
                - `erp__recommend_flow(erp, useCase)` – Flow details + validation rules
                - `erp__diagnose_error(erp, errorMessage)` – Error patterns when needed

                **Test Coverage Needed**:
                1. **Happy Path** - Successful operations with valid data
                2. **Edge Cases** - Boundary conditions, special characters, unicode, empty strings
                3. **Error Cases** - All validation errors, API errors, business logic errors
                4. **Performance** - Bulk operations, pagination, timeout handling
                5. **Security** - SQL injection attempts, XSS attempts, auth bypass attempts
                6. **Idempotency** - Duplicate handling, retry behavior

                Please design a complete test plan that I can implement in Kotlin tests.
                """),

            new ChatMessage(ChatRole.Assistant, $"""
                I'll design a comprehensive test plan for **{operationName}**. Let me analyze what we're testing:

                **Initial Assessment**:
                {AnalyzeOperationType(operationName)}

                **Test Design Strategy**:
                1. First, I'll use `erp__query_knowledge(erp='acumatica', query=operationName)` to understand fields and rules
                2. Then use `erp__recommend_flow` for flow-specific validation rules
                3. Synthesize comprehensive scenarios from rules and constants
                4. Create detailed test plan organized by test type
                5. Include actual test data and assertions you can copy-paste into Kotlin

                **Test Data Approach**:
                - Use timestamp-based unique data to avoid conflicts
                - Test against real instance: http://63.32.187.185/StampliAcumaticaDB
                - Clean up test data where possible (or use descriptive names)

                Let me start gathering operation details...
                """),

            new ChatMessage(ChatRole.User,
                """
                Excellent! Structure the test plan as:

                **📋 Test Summary**
                - Total scenarios: [count]
                - Priority breakdown: P0/P1/P2
                - Estimated execution time

                **✅ Happy Path Tests** (P0)
                - Test name, test data, expected result

                **⚠️ Edge Case Tests** (P1)
                - Boundary conditions, special characters, etc.

                **❌ Error Validation Tests** (P0)
                - One test per validation rule

                **🏎️ Performance Tests** (P2)
                - Bulk operations, pagination scenarios

                **🔒 Security Tests** (P1)
                - Injection attempts, malicious data

                For each test, provide:
                - Test name (Kotlin style: `operation action context`)
                - Test data (actual values to use)
                - Expected result (with exact error messages where applicable)
                - Priority (P0/P1/P2)

                Use MCP tools to get accurate validation rules and error messages.
                """),

            new ChatMessage(ChatRole.Assistant,
                """
                Perfect! I'll provide a detailed, actionable test plan you can implement directly in Kotlin.
                Each test will have concrete data, exact assertions, and priority levels.
                I'll use real validation rules from the MCP knowledge base, not assumptions.
                Gathering comprehensive operation details now...
                """)
        };
    }

    private static string AnalyzeOperationType(string operationName)
    {
        var lower = operationName.ToLowerInvariant();
        var analysis = new List<string>();

        // Operation category
        if (lower.StartsWith("export"))
        {
            analysis.Add("**Operation type**: Export/Create (writes data to Acumatica)");
            analysis.Add("**Primary tests**: Validation rules, duplicate handling, idempotency");
            analysis.Add("**Key risks**: Data corruption, duplicate creation, validation bypass");
        }
        else if (lower.StartsWith("get") || lower.StartsWith("retrieve"))
        {
            analysis.Add("**Operation type**: Import/Read (reads data from Acumatica)");
            analysis.Add("**Primary tests**: Pagination, filtering, data accuracy");
            analysis.Add("**Key risks**: Performance with large datasets, missing data, incorrect filters");
        }
        else if (lower.Contains("matching") || lower.Contains("find"))
        {
            analysis.Add("**Operation type**: Lookup/Search (finds existing data)");
            analysis.Add("**Primary tests**: Search accuracy, null handling, case sensitivity");
            analysis.Add("**Key risks**: False positives, false negatives, performance");
        }
        else if (lower.Contains("connect") || lower.Contains("auth"))
        {
            analysis.Add("**Operation type**: Authentication/Connection");
            analysis.Add("**Primary tests**: Valid/invalid credentials, session management");
            analysis.Add("**Key risks**: Security vulnerabilities, credential leakage");
        }

        // Entity identification
        if (lower.Contains("vendor"))
        {
            analysis.Add("**Entity**: Vendor");
            analysis.Add("**Common fields**: vendorName (max 60), vendorID (max 15), stampliLink, email");
        }
        else if (lower.Contains("bill") || lower.Contains("aptransaction"))
        {
            analysis.Add("**Entity**: Bill/APTransaction");
            analysis.Add("**Common fields**: billID, vendorRef, amount, date, stampliLink");
        }
        else if (lower.Contains("payment"))
        {
            analysis.Add("**Entity**: Payment");
            analysis.Add("**Common fields**: paymentID, billRefs, amount, paymentDate");
        }

        // Expected test count estimate
        var estimatedTests = 5; // Base
        if (lower.StartsWith("export")) estimatedTests += 10; // More validations for writes
        if (lower.StartsWith("get")) estimatedTests += 5; // Pagination scenarios
        analysis.Add($"**Estimated scenarios**: {estimatedTests}-{estimatedTests + 5} tests");

        return string.Join("\n", analysis);
    }
}
