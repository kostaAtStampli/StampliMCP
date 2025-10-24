# Test-Driven Development Workflow for Kotlin ERP Harness

## Core TDD Philosophy

**Write Test First → Verify Failure → Implement → Verify Success**

## Complete TDD Workflow

### Step 1: Query MCP for Operation Intelligence

```bash
# Query MCP for operation details
Tool: get_operation
Parameters: { "methodName": "exportVendor" }

# Returns:
- Required fields with validation rules
- Error messages for assertions
- Code pointers to scan
- Golden test examples
```

### Step 2: Scan Legacy Code (Using MCP Pointers)

```bash
# MCP provides exact locations
"scanThese": [
  {
    "file": "finsys-drivers/acumatica/.../AcumaticaDriver.java",
    "lines": "386-398",
    "purpose": "Main export method"
  },
  {
    "file": "finsys-drivers/acumatica/.../CreateVendorHandler.java",
    "lines": "22-90",
    "purpose": "Validation logic"
  }
]

# Scan these files to understand:
- Validation rules
- Error messages
- Business logic
- API payload format
```

### Step 3: Write Test FIRST (Red Phase)

```kotlin
// Location: kotlin-erp-harness/src/test/kotlin/.../KotlinAcumaticaDriverTest.kt

class KotlinAcumaticaDriverTest {

    private lateinit var driver: KotlinAcumaticaDriver
    private lateinit var connectionProperties: Map<String, String>

    @BeforeEach
    fun setUp() {
        driver = KotlinAcumaticaDriver()
        connectionProperties = mapOf(
            "hostname" to "http://63.32.187.185/StampliAcumaticaDB",
            "user" to "admin",
            "password" to "Password1"
        )
    }

    @Test
    fun `export vendor successfully`() {
        // Arrange - Build request exactly like legacy
        val request = ExportVendorRequest().apply {
            subsidiary = "StampliCompany"
            dualDriverName = "com.stampli.kotlin.driver.KotlinAcumaticaDriver"
            this.connectionProperties = this@KotlinAcumaticaDriverTest.connectionProperties
            finSysBridgeTransferredObject = FinSysBridgeTransferredObject()

            rawData = mapOf(
                "vendorName" to "Test Vendor ${System.currentTimeMillis()}",
                "vendorId" to "V${System.currentTimeMillis()}",
                "stampliLink" to "https://app.stampli.com/link/test",
                "email" to "vendor@test.com"
            )
        }

        // Act
        val response = driver.exportVendor(request)

        // Assert - Use exact pattern from legacy
        assertNull(response.error, "Unexpected error: ${response.error}")
        assertNotNull(response.response)
        assertNotNull(response.response.id)
    }

    @Test
    fun `export vendor validation error - missing name`() {
        // Arrange
        val request = createRequest(vendorName = "") // Empty name

        // Act
        val response = driver.exportVendor(request)

        // Assert - Use exact error message from MCP
        assertNotNull(response.error)
        assertEquals("vendorName is required", response.error)
    }

    @Test
    fun `export vendor idempotency - duplicate returns existing`() {
        // Create vendor first time
        val request1 = createRequest("DupTest", "https://stampli.com/dup")
        val response1 = driver.exportVendor(request1)
        assertNull(response1.error)
        val vendorId1 = response1.response.id

        // Create same vendor second time
        val request2 = createRequest("DupTest", "https://stampli.com/dup")
        val response2 = driver.exportVendor(request2)

        // Should succeed with same ID (idempotent)
        assertNull(response2.error)
        assertEquals(vendorId1, response2.response.id)
    }

    @Test
    fun `export vendor link mismatch error`() {
        // Create vendor with one link
        val request1 = createRequest("LinkTest", "https://stampli.com/link1")
        driver.exportVendor(request1)

        // Try to create same vendor with different link
        val request2 = createRequest("LinkTest", "https://stampli.com/link2")
        val response2 = driver.exportVendor(request2)

        // Should fail with link mismatch error
        assertNotNull(response2.error)
        assertTrue(response2.error.contains("different Stampli link"))
    }
}
```

### Step 4: Run Test - Verify FAILURE (TDD Requirement)

```bash
./gradlew :kotlin-erp-harness:test --tests KotlinAcumaticaDriverTest

# Expected: Tests FAIL because KotlinAcumaticaDriver doesn't exist yet
# This confirms we're doing true TDD
```

### Step 5: Implement Feature (Green Phase)

```kotlin
// Location: kotlin-erp-harness/src/main/kotlin/.../KotlinAcumaticaDriver.kt

class KotlinAcumaticaDriver : IDualFinsysDriver {

    private val apiCallerFactory = ApiCallerFactory()

    override fun exportVendor(request: ExportVendorRequest): ExportResponse {
        val response = ExportResponse()

        val rawData = request.rawData
        if (rawData == null) {
            response.error = "Missing vendor data"
            return response
        }

        // Validation (from MCP error catalog)
        val vendorName = rawData["vendorName"]
        if (vendorName.isNullOrBlank()) {
            response.error = "vendorName is required"
            return response
        }

        if (vendorName.length > 60) {
            response.error = "vendorName exceeds maximum length of 60 characters"
            return response
        }

        val stampliLink = rawData["stampliLink"]
        if (stampliLink.isNullOrBlank()) {
            response.error = "stampliurl is required"
            return response
        }

        // Check for duplicates (risk control)
        val existingVendor = findVendorByLink(request, stampliLink)
        if (existingVendor != null) {
            if (existingVendor.link == stampliLink) {
                // Idempotent - return existing
                response.response = CsvLinkBridgeObject().apply {
                    id = existingVendor.id
                }
                return response
            } else {
                // Link mismatch
                response.error = "Vendor already exists with different Stampli link"
                return response
            }
        }

        // Build JSON payload (reuse legacy mapper)
        val jsonPayload = VendorPayloadMapper.mapToAcumaticaJson(rawData, vendorName, stampliLink)

        // Make API call with authentication
        val apiCaller = apiCallerFactory.createPutRestApiCaller(
            request,
            AcumaticaEndpoint.VENDOR,
            AcumaticaUrlSuffixAssembler(),
            jsonPayload
        )

        val apiResponse = AcumaticaAuthenticator.authenticatedApiCall(
            request,
            apiCallerFactory
        ) { apiCaller.call() }

        if (!apiResponse.isSuccessful) {
            response.error = "Acumatica returned ${apiResponse.responseCode}: ${apiResponse.content}"
            return response
        }

        // Parse response and return
        val vendorId = extractVendorId(apiResponse.content)
        response.response = CsvLinkBridgeObject().apply {
            id = vendorId
        }

        return response
    }
}
```

### Step 6: Run Test - Verify SUCCESS

```bash
./gradlew :kotlin-erp-harness:test --tests KotlinAcumaticaDriverTest

# Expected: All tests PASS
# This confirms implementation is correct
```

## Key Testing Patterns

### Pattern 1: Request Building

```kotlin
// Always build requests exactly like legacy
val request = ExportVendorRequest().apply {
    subsidiary = "StampliCompany"
    dualDriverName = "com.stampli.kotlin.driver.KotlinAcumaticaDriver"
    connectionProperties = testConnectionProperties
    finSysBridgeTransferredObject = FinSysBridgeTransferredObject()
    rawData = mapOf(/* vendor data */)
}
```

### Pattern 2: Assertions

```kotlin
// Success assertion
assertNull(response.error, "Unexpected error: ${response.error}")
assertNotNull(response.response)

// Error assertion
assertNotNull(response.error)
assertTrue(response.error.contains("expected message"))

// Exact message assertion
assertEquals("vendorName is required", response.error)
```

### Pattern 3: Test Data Isolation

```kotlin
// Use timestamps to avoid conflicts
val vendorName = "Test Vendor ${System.currentTimeMillis()}"
val vendorId = "V${System.currentTimeMillis()}"
```

## Golden Test Examples to Copy

| Operation | Legacy Test File | Key Tests |
|-----------|-----------------|-----------|
| exportVendor | AcumaticaDriverCreateVendorITest.java:30-300 | Success, Idempotency, Link mismatch, Validation |
| getVendors | AcumaticaDriverITest.java:28-60 | Import with pagination |
| exportAPTransaction | AcumaticaDriverExportBillITest.java | Bill export patterns |

## Common Pitfalls to Avoid

1. **Don't skip the RED phase** - Test must fail first
2. **Use exact error messages** - From MCP error catalog
3. **Don't throw exceptions** - Use response.error
4. **Build requests properly** - Include all required fields
5. **Test against real instance** - 63.32.187.185

## Test Execution Commands

```bash
# Run all tests
./gradlew :kotlin-erp-harness:test

# Run specific test class
./gradlew :kotlin-erp-harness:test --tests KotlinAcumaticaDriverTest

# Run specific test method
./gradlew :kotlin-erp-harness:test --tests "KotlinAcumaticaDriverTest.export vendor successfully"

# Run with debugging
./gradlew :kotlin-erp-harness:test --debug-jvm
```

## Success Criteria

✅ Test written before implementation
✅ Test fails initially (RED)
✅ Implementation makes test pass (GREEN)
✅ All assertions use exact messages from MCP
✅ Tests run against live test instance
✅ No flaky tests - proper data isolation