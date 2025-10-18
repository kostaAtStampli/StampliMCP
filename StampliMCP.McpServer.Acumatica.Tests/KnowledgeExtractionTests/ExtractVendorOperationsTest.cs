using FluentAssertions;

namespace StampliMCP.McpServer.Acumatica.Tests.KnowledgeExtractionTests;

/// <summary>
/// Extract vendor operations knowledge - Create, Update, Search, Validate
/// Uses MCP tool for two-scan enforcement: Scan 1 → challenge_scan_findings → Scan 2 → vendor-operations.json
/// </summary>
public sealed class ExtractVendorOperationsTest : KnowledgeExtractionTestBase
{
    [Fact]
    public async Task Extract_Vendor_Operations()
    {
        // Create isolated test directory
        var testDir = CreateTestDirectory("vendor");

        // Define challenge areas for MCP tool
        var challengeAreas = new[]
        {
            "validation_rules",
            "line_numbers",
            "constants",
            "operation_count",
            "test_coverage"
        };

        // Run two-scan extraction with MCP tool enforcement
        var knowledge = await RunTwoScanExtraction(
            "vendor_operations",
            GetScan1Questions(),
            challengeAreas,
            testDir);

        // Update knowledge file
        UpdateKnowledgeFile("vendor-operations.json", knowledge);

        // Assertions
        knowledge.Should().NotBeNull("Knowledge should be extracted");
        knowledge.RequiredFields.Should().NotBeEmpty("Should find vendor field requirements");
        knowledge.ScanThese.Should().NotBeEmpty("Should identify files to scan");
        knowledge.Scan2Challenges.Should().BeGreaterThanOrEqualTo(0, "Scan 2 may challenge Scan 1 findings");

        Console.WriteLine($"\n=== Vendor operations extraction completed successfully ===");
        Console.WriteLine($"=== Logs saved to: {testDir} ===");
    }

    protected override string[] GetScan1Questions()
    {
        // These are discovery questions for Scan 1
        // The MCP tool will generate challenge questions for Scan 2
        return new[]
        {
            @"Find ALL vendor-related operations in C:\STAMPLI4\core\finsys-drivers\acumatica\src\main\java\com\stampli\finsys\drivers\acumatica\operations\vendor.
              List all Java classes with vendor in the name.
              Return JSON:
              {
                ""vendorOperations"": [
                  {""file"": ""CreateVendor.java"", ""lines"": X, ""purpose"": ""...""},
                  {""file"": ""UpdateVendor.java"", ""lines"": X, ""purpose"": ""...""},
                  ...
                ]
              }",

            @"Scan CreateVendor.java in C:\STAMPLI4\core\finsys-drivers\acumatica.
              Find: execute() method, validation logic, API payload structure.
              Return JSON:
              {
                ""createVendor"": {
                  ""executeMethod"": {""lines"": ""X-Y"", ""logic"": ""...""},
                  ""validations"": [{""field"": ""vendorName"", ""maxLength"": X}, ...],
                  ""apiEndpoint"": ""..."",
                  ""payloadStructure"": {""fields"": [...]}
                }
              }",

            @"Scan UpdateVendor.java.
              How does it differ from CreateVendor? What fields can be updated?
              Return JSON:
              {
                ""updateVendor"": {
                  ""updatableFields"": [...],
                  ""immutableFields"": [...],
                  ""diffFromCreate"": ""...""
                }
              }",

            @"Find vendor search/query operations in C:\STAMPLI4.
              Look for: SearchVendor, QueryVendor, GetVendor, FindVendor.
              Return JSON:
              {
                ""vendorSearch"": [
                  {""file"": ""..."", ""searchBy"": [""vendorId"", ""name"", ...], ""pagination"": true/false}
                ]
              }",

            @"Find vendor validation utilities.
              Look for: VendorValidator, VendorUtils, VendorHelper classes.
              Return JSON:
              {
                ""vendorUtilities"": [
                  {""class"": ""..."", ""methods"": [...], ""purpose"": ""...""}
                ]
              }",

            @"Check the Kotlin vendor implementation in C:\STAMPLI4\core\finsys-modern\kotlin-acumatica-driver.
              Compare CreateVendorHandler.kt to Java CreateVendor.java.
              Return JSON:
              {
                ""kotlinVsJava"": {
                  ""kotlinFile"": ""CreateVendorHandler.kt"",
                  ""javaFile"": ""CreateVendor.java"",
                  ""differences"": [...],
                  ""improvements"": [...]
                }
              }",

            @"Find vendor-related test files.
              Look in both Java tests (finsys-drivers) and Kotlin tests (finsys-modern).
              Return JSON:
              {
                ""vendorTests"": {
                  ""java"": [{""file"": ""..."", ""testCount"": X}],
                  ""kotlin"": [{""file"": ""..."", ""testCount"": X}]
                }
              }",

            @"Find vendor error handling patterns.
              Look for: VendorException, vendor error messages, error codes.
              Return JSON:
              {
                ""vendorErrors"": [
                  {""errorCode"": ""..."", ""message"": ""..."", ""handling"": ""...""}
                ]
              }"
        };
    }

    protected override string[] GetChallengeAreas()
    {
        // These areas will be challenged by the MCP tool in Scan 2
        return new[]
        {
            "validation_rules",  // Verify all validation rules are found
            "line_numbers",      // Verify line numbers are accurate
            "constants",         // Find all constants (may have missed some)
            "operation_count",   // Count all vendor operations accurately
            "test_coverage"      // Verify test coverage claims
        };
    }
}