using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.KnowledgeExtractionTests;

/// <summary>
/// Extract payment flows knowledge - Export, Status, Sync, Reconciliation
/// Uses MCP tool for two-scan enforcement: Scan 1 → challenge_scan_findings → Scan 2 → payment-flows.json
/// </summary>
public sealed class ExtractPaymentFlowsTest : KnowledgeExtractionTestBase
{
    [Fact]
    public async Task Extract_Payment_Flows()
    {
        // Create isolated test directory
        var testDir = CreateTestDirectory("payment");

        // Run two-scan extraction with MCP tool enforcement
        var knowledge = await RunTwoScanExtraction(
            "payment_flows",
            GetScan1Questions(),
            GetChallengeAreas(),
            testDir);

        // Update knowledge file
        UpdateKnowledgeFile("payment-flows.json", knowledge);

        // Assertions
        knowledge.Should().NotBeNull("Knowledge should be extracted");
        knowledge.RequiredFields.Should().NotBeEmpty("Should find payment field requirements");
        knowledge.ScanThese.Should().NotBeEmpty("Should identify files to scan");
        knowledge.Scan2Challenges.Should().BeGreaterThanOrEqualTo(0, "Scan 2 may challenge Scan 1 findings");

        Console.WriteLine($"\n=== Payment flows extraction completed successfully ===");
        Console.WriteLine($"=== Logs saved to: {testDir} ===");
    }

    protected override string[] GetScan1Questions()
    {
        // These are discovery questions for Scan 1
        // The MCP tool will generate challenge questions for Scan 2
        return new[]
        {
            @"Find ALL payment-related operations in C:\STAMPLI4\core\finsys-drivers\acumatica.
              Look for: ExportPayment, PaymentStatus, PaymentSync, CreatePayment, UpdatePayment.
              Return JSON:
              {
                ""paymentOperations"": [
                  {""file"": ""ExportPayment.java"", ""lines"": X, ""purpose"": ""Export payment to ERP""},
                  {""file"": ""GetPaymentStatus.java"", ""lines"": X, ""purpose"": ""Check payment status""},
                  ...
                ]
              }",

            @"Scan ExportPayment.java operation.
              Find: execute() method, payment data structure, API endpoints, validation.
              Return JSON:
              {
                ""exportPayment"": {
                  ""executeMethod"": {""lines"": ""X-Y"", ""logic"": ""...""},
                  ""paymentFields"": [""invoiceId"", ""amount"", ""paymentMethod"", ...],
                  ""validations"": [{""field"": ""amount"", ""rule"": ""positive number""}, ...],
                  ""apiEndpoint"": ""/entity/Default/20.200.001/Payment""
                }
              }",

            @"Find payment status synchronization logic.
              Look for: GetPaymentStatus, SyncPaymentStatus, PaymentStatusPoller.
              Return JSON:
              {
                ""paymentStatusSync"": {
                  ""operations"": [{""class"": ""..."", ""purpose"": ""...""}],
                  ""pollingInterval"": ""..."",
                  ""statusValues"": [""PENDING"", ""APPROVED"", ""REJECTED"", ...]
                }
              }",

            @"Find payment reconciliation patterns.
              Look for: ReconcilePayment, PaymentMatcher, PaymentReconciliation.
              Return JSON:
              {
                ""reconciliation"": {
                  ""matchingFields"": [""invoiceNumber"", ""amount"", ...],
                  ""reconciliationRules"": [...],
                  ""tolerances"": {""amount"": ""..."", ""date"": ""...""}
                }
              }",

            @"Find payment batch processing.
              Look for: BatchPayment, PaymentBatch, BulkPayment operations.
              Return JSON:
              {
                ""batchPayment"": {
                  ""supported"": true/false,
                  ""maxBatchSize"": X,
                  ""batchOperations"": [{""file"": ""..."", ""purpose"": ""...""}]
                }
              }",

            @"Find payment error handling and retry logic.
              Look for payment-specific exceptions, retry patterns, error recovery.
              Return JSON:
              {
                ""paymentErrors"": [
                  {""errorType"": ""PaymentException"", ""retryable"": true/false, ""maxRetries"": X},
                  {""errorType"": ""InsufficientFunds"", ""handling"": ""...""},
                  ...
                ]
              }",

            @"Find payment-related test coverage.
              Look in test directories for payment test files.
              Return JSON:
              {
                ""paymentTests"": [
                  {""file"": ""ExportPaymentTest.java"", ""testMethods"": X, ""coverage"": ""...""},
                  ...
                ]
              }"
        };
    }

    protected override string[] GetChallengeAreas()
    {
        // These areas will be challenged by the MCP tool in Scan 2
        return new[]
        {
            "validation_rules",   // Verify payment validation rules
            "line_numbers",       // Verify line numbers are accurate
            "constants",          // Find payment-related constants
            "operation_count",    // Count all payment operations
            "test_coverage"       // Verify test coverage for payments
        };
    }
}