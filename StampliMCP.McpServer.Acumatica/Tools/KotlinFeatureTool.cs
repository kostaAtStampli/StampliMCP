using System.ComponentModel;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class KotlinFeatureTool
{
    // Called internally by KotlinTddWorkflowTool
    internal static Task<object> ImplementKotlinFeature(
        string featureDescription)
    {
        var workflow = new
        {
            // Metadata
            feature = featureDescription,
            workflowVersion = "2.0-nuclear",
            enforcementLevel = "strict",
            tddCompliance = "mandatory",

            // 7-Step Enforced Workflow
            steps = new object[]
            {
                new
                {
                    step = 1,
                    name = "Discover Operations",
                    instruction = $"""
                    Analyze the feature description: "{featureDescription}"

                    1. Identify which Acumatica operations are needed
                    2. Use search_operations tool to find relevant operations
                    3. Check Knowledge/categories.json for operation groupings
                    4. Identify primary operation (e.g., exportVendor, getVendors)
                    5. Identify supporting operations (e.g., duplicate checks, validations)

                    Example operations:
                    - "vendor" → exportVendor, getVendors, getMatchingVendorByStampliLink
                    - "bill" → exportAPTransaction, retrieveInvoiceByReferenceId
                    - "payment" → exportBillPayment, retrieveBillPayments
                    """,
                    validation = new
                    {
                        required = "List of operation method names identified",
                        example = new[] { "exportVendor", "getMatchingVendorByStampliLink" },
                        failIf = "No operations identified or wrong operations selected"
                    },
                    nextTool = "get_operation_details",
                    blockUntil = "At least 1 operation identified"
                },

                new
                {
                    step = 2,
                    name = "Query Operation Details",
                    instruction = """
                    For EACH operation identified in Step 1:

                    1. Call get_operation_details(methodName) for each operation
                    2. Collect:
                       - Required fields with max lengths
                       - Exact error messages (MUST be identical to legacy)
                       - File pointers to legacy code (scanFiles)
                       - Golden pattern references
                       - Test examples

                    3. Store validation rules for Step 4 (test writing)

                    CRITICAL: Error messages MUST be EXACT matches.
                    Example: "vendorName is required" NOT "Vendor name is required"
                    """,
                    validation = new
                    {
                        required = "All required fields, error messages, and file pointers retrieved",
                        example = new
                        {
                            operation = "exportVendor",
                            requiredFields = new[] { "vendorName (max 60)", "stampliLink" },
                            errors = new[] { "vendorName is required", "vendorName exceeds maximum length of 60 characters" },
                            scanFiles = new[] { "CreateVendorHandler.java:22-90", "GOLDEN_PATTERNS.md:61-115" }
                        },
                        failIf = "Missing required fields or error messages"
                    },
                    nextTool = "Read tool to scan legacy files",
                    blockUntil = "All operations have details retrieved"
                },

                new
                {
                    step = 3,
                    name = "Scan Legacy Code",
                    instruction = """
                    Using file pointers from Step 2:

                    1. Read legacy Java files at EXACT line ranges
                       Path: C:\STAMPLI4\core\{file}
                       Example: C:\STAMPLI4\core\finsys-drivers\acumatica\CreateVendorHandler.java:22-90

                    2. Extract patterns:
                       - Validation order (required first, then length)
                       - Error message formatting
                       - API call pattern (ALWAYS AcumaticaAuthenticator.authenticatedApiCall)
                       - JSON structure (Acumatica format: {"field": {"value": "data"}})
                       - Response building

                    3. Read Knowledge/kotlin/GOLDEN_PATTERNS.md for copy-paste patterns

                    4. Read Knowledge/kotlin/error-patterns-kotlin.json for exact error messages
                    """,
                    validation = new
                    {
                        required = "Patterns extracted from legacy code and golden patterns",
                        mustExtract = new[]
                        {
                            "Validation pattern",
                            "Authentication pattern (AcumaticaAuthenticator)",
                            "Error handling pattern (response.error, NOT exceptions)",
                            "JSON structure",
                            "Test setup pattern"
                        },
                        failIf = "Patterns not extracted or incomplete"
                    },
                    nextTool = "Write tool to create test file",
                    blockUntil = "All patterns extracted and documented"
                },

                new
                {
                    step = 4,
                    name = "Write Tests (TDD RED Phase)",
                    instruction = """
                    Create tests in kotlin-erp-harness module:
                    Location: /mnt/c/STAMPLI4/core/kotlin-erp-harness/src/test/kotlin/com/stampli/kotlin/driver/KotlinAcumaticaDriverTest.kt

                    1. Use test setup pattern from Knowledge/kotlin/test-config-kotlin.json
                    2. Write tests for EACH validation rule from Step 2
                    3. Use EXACT error messages from get_operation_details
                    4. Write success test with REAL test instance
                    5. Use timestamp-based unique data to avoid conflicts

                    Example:
                    ```kotlin
                    @Test
                    fun `exportVendor validates required vendorName`() {
                        val request = createRequest(vendorName = "")
                        val response = driver.exportVendor(request)

                        // EXACT error from Step 2
                        assertEquals("vendorName is required", response.error)
                    }

                    @Test
                    fun `exportVendor creates vendor successfully`() {
                        val request = createRequest(
                            vendorName = "Test Vendor ${System.currentTimeMillis()}"
                        )
                        val response = driver.exportVendor(request)

                        assertNull(response.error)
                        assertNotNull(response.response?.id)
                    }
                    ```

                    6. Run tests: dotnet build -c Debug /mnt/c/STAMPLI4/core/kotlin-erp-harness
                    """,
                    validation = new
                    {
                        required = "Tests written and FAIL when run (TDD RED phase)",
                        testCount = "Minimum: 1 success test + 1 validation test per required field",
                        mustFail = true,
                        failReason = "Method not implemented yet (this is CORRECT for TDD)",
                        blockIf = "Tests PASS (means implementation exists - wrong phase!)"
                    },
                    command = "dotnet build or gradlew test",
                    expectedResult = "TESTS FAIL - Not implemented (RED phase ✓)",
                    nextTool = "Edit tool to implement code",
                    blockUntil = "Tests written and failing (RED confirmed)"
                },

                new
                {
                    step = 5,
                    name = "Implement Kotlin Code (TDD GREEN Phase)",
                    instruction = """
                    Implement in kotlin-erp-harness module:
                    Location: /mnt/c/STAMPLI4/core/kotlin-erp-harness/src/main/kotlin/com/stampli/kotlin/driver/KotlinAcumaticaDriver.kt

                    1. Copy pattern from Knowledge/kotlin/GOLDEN_PATTERNS.md
                    2. Follow EXACT structure:

                    ```kotlin
                    override fun exportVendor(request: ExportVendorRequest): ExportResponse {
                        val response = ExportResponse()

                        val raw = request.rawData
                        if (raw == null) {
                            response.error = "Missing vendor data"
                            return response
                        }

                        // Validation - EXACT errors from Step 2
                        val vendorName = raw["vendorName"]
                        if (vendorName.isNullOrBlank()) {
                            response.error = "vendorName is required"  // EXACT
                            return response
                        }

                        if (vendorName.length > 60) {
                            response.error = "vendorName exceeds maximum length of 60 characters"  // EXACT
                            return response
                        }

                        // API call - ALWAYS use authenticator
                        val apiCaller = apiCallerFactory.createPutRestApiCaller(
                            request, AcumaticaEndpoint.VENDOR, AcumaticaUrlSuffixAssembler(), body
                        )

                        val apiResponse = AcumaticaAuthenticator.authenticatedApiCall(
                            request, apiCallerFactory
                        ) { apiCaller.call() }

                        if (!apiResponse.isSuccessful) {
                            response.error = "Acumatica returned ${apiResponse.responseCode}"
                            return response
                        }

                        response.response = CsvLinkBridgeObject().apply {
                            id = extractId(apiResponse.content)
                        }
                        return response
                    }
                    ```

                    3. NEVER throw exceptions for business errors
                    4. ALWAYS set response.error instead
                    5. Reuse legacy helper classes (VendorPayloadMapper, etc.)
                    """,
                    validation = new
                    {
                        required = "Code implemented following golden patterns",
                        mustHave = new[]
                        {
                            "AcumaticaAuthenticator.authenticatedApiCall usage",
                            "Exact error messages from Step 2",
                            "No exceptions thrown for validation",
                            "Response object pattern (error OR response, not both)"
                        },
                        failIf = "Code doesn't follow patterns or missing authentication"
                    },
                    nextTool = "Bash tool to run tests",
                    blockUntil = "Implementation complete"
                },

                new
                {
                    step = 6,
                    name = "Verify Tests Pass (TDD GREEN Phase)",
                    instruction = """
                    Run tests to verify TDD GREEN phase:

                    1. Run: dotnet build -c Debug /mnt/c/STAMPLI4/core/kotlin-erp-harness
                    2. Or: ./gradlew :kotlin-erp-harness:test

                    3. Verify ALL tests PASS:
                       ✓ Validation tests pass (exact error messages)
                       ✓ Success tests pass (creates vendor in test instance)
                       ✓ No compilation errors

                    4. If tests FAIL:
                       - Check error messages match EXACTLY
                       - Verify authentication pattern used
                       - Check JSON structure
                       - Verify no exceptions thrown
                       - Fix implementation and re-run
                    """,
                    validation = new
                    {
                        required = "ALL tests PASS (TDD GREEN phase)",
                        mustPass = true,
                        blockIf = "Any test fails - implementation incomplete",
                        hint = "If failing, compare implementation with GOLDEN_PATTERNS.md exactly"
                    },
                    command = "dotnet build or gradlew test",
                    expectedResult = "ALL TESTS PASS - GREEN phase ✓",
                    nextTool = "None - proceed to Step 7",
                    blockUntil = "100% tests passing"
                },

                new
                {
                    step = 7,
                    name = "Report Completion",
                    instruction = """
                    Generate completion summary:

                    1. List operations implemented
                    2. Report test statistics (count, passing rate)
                    3. List files modified
                    4. Document validation rules applied
                    5. Confirm TDD workflow completed (RED → GREEN)

                    Format:
                    ```
                    ✓ Feature Complete: {feature description}

                    Operations Implemented:
                    - exportVendor
                    - getMatchingVendorByStampliLink

                    Tests:
                    - Total: 12
                    - Passing: 12 (100%)
                    - Coverage: All validation rules + success paths

                    Files Modified:
                    - KotlinAcumaticaDriver.kt: Added exportVendor implementation
                    - KotlinAcumaticaDriverTest.kt: Added 12 tests

                    Validation Rules:
                    - vendorName: required, max 60 chars
                    - stampliLink: required
                    - Error messages: Exact match with legacy

                    TDD Workflow:
                    ✓ Step 4: Tests failed (RED phase)
                    ✓ Step 6: Tests passed (GREEN phase)
                    ```
                    """,
                    validation = new
                    {
                        required = "Summary generated with all metrics",
                        mustInclude = new[] { "Operations", "Tests", "Files", "Validation", "TDD confirmation" }
                    },
                    nextTool = "None - DONE",
                    blockUntil = "Summary complete"
                }
            },

            // Knowledge Resources
            resources = new
            {
                patterns = "Knowledge/kotlin/GOLDEN_PATTERNS.md",
                errors = "Knowledge/kotlin/error-patterns-kotlin.json",
                workflow = "Knowledge/kotlin/03_AI_TDD_WORKFLOW.xml",
                architecture = "Knowledge/kotlin/KOTLIN_ARCHITECTURE.md",
                testConfig = "Knowledge/kotlin/test-config-kotlin.json",
                integration = "Knowledge/kotlin/kotlin-integration.json",
                methodSignatures = "Knowledge/kotlin/method-signatures.json",
                completeAnalysis = "Knowledge/kotlin/ACUMATICA_COMPLETE_ANALYSIS.md"
            },

            // Critical Enforcement Rules
            enforcement = new
            {
                rule1 = "NEVER skip TDD RED phase - tests MUST fail first",
                rule2 = "NEVER skip TDD GREEN phase - tests MUST pass after implementation",
                rule3 = "ALWAYS use exact error messages from get_operation_details",
                rule4 = "ALWAYS use AcumaticaAuthenticator.authenticatedApiCall",
                rule5 = "NEVER throw exceptions for validation/business errors",
                rule6 = "ALWAYS set response.error instead of throwing",
                rule7 = "ALWAYS follow golden patterns from GOLDEN_PATTERNS.md"
            },

            // Test Instance
            testInstance = new
            {
                url = "http://63.32.187.185/StampliAcumaticaDB",
                username = "admin",
                password = "Password1",
                subsidiary = "StampliCompany",
                note = "Use timestamp-based unique data to avoid conflicts"
            }
        };

        return Task.FromResult<object>(workflow);
    }
}
