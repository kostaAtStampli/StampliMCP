# Golden Patterns - Copy These Exactly

## Pattern 1: Vendor Export Handler

### Legacy Pattern (CreateVendorHandler.java:22-90)

```java
public ExportResponse execute(ApiCallerFactory apiCallerFactory, ExportVendorRequest request) {
    ExportResponse response = new ExportResponse();

    Map<String, String> raw = request.getRawData();
    if (raw == null) {
        response.setError("Missing vendor data");
        return response;
    }

    // Validation
    String vendorName = raw.get("vendorName");
    if (StringUtils.isBlank(vendorName)) {
        response.setError("vendorName is required");
        return response;
    }

    if (vendorName.length() > 60) {
        response.setError("vendorName exceeds maximum length of 60 characters");
        return response;
    }

    String stampliLink = raw.get("stampliLink");
    if (StringUtils.isBlank(stampliLink)) {
        response.setError("stampliurl is required");
        return response;
    }

    // Build JSON payload
    String body = VendorPayloadMapper.mapToAcumaticaJson(raw, vendorName, stampliLink);

    // API Call with authentication
    ApiCaller apiCaller = apiCallerFactory.createPutRestApiCaller(
        request, AcumaticaEndpoint.VENDOR, new AcumaticaUrlSuffixAssembler(), body
    );

    ResponseData createResp = AcumaticaAuthenticator.authenticatedApiCall(
        request, apiCallerFactory, apiCaller::call
    );

    if (!createResp.isSuccessful()) {
        response.setError("Acumatica returned " + createResp.getResponseCode());
        return response;
    }

    // Extract vendor ID and return
    String vendorId = extractVendorId(createResp.getContent());
    CsvLinkBridgeObject cl = new CsvLinkBridgeObject();
    cl.setId(vendorId);
    response.setResponse(cl);
    return response;
}
```

### Kotlin Pattern (Copy This)

```kotlin
fun exportVendor(request: ExportVendorRequest): ExportResponse {
    val response = ExportResponse()

    val raw = request.rawData
    if (raw == null) {
        response.error = "Missing vendor data"
        return response
    }

    // Validation - exact same rules
    val vendorName = raw["vendorName"]
    if (vendorName.isNullOrBlank()) {
        response.error = "vendorName is required"
        return response
    }

    if (vendorName.length > 60) {
        response.error = "vendorName exceeds maximum length of 60 characters"
        return response
    }

    val stampliLink = raw["stampliLink"]
    if (stampliLink.isNullOrBlank()) {
        response.error = "stampliurl is required"
        return response
    }

    // Build JSON payload - reuse legacy mapper
    val body = VendorPayloadMapper.mapToAcumaticaJson(raw, vendorName, stampliLink)

    // API Call with authentication - exact same pattern
    val apiCaller = apiCallerFactory.createPutRestApiCaller(
        request, AcumaticaEndpoint.VENDOR, AcumaticaUrlSuffixAssembler(), body
    )

    val createResp = AcumaticaAuthenticator.authenticatedApiCall(
        request, apiCallerFactory
    ) { apiCaller.call() }

    if (!createResp.isSuccessful) {
        response.error = "Acumatica returned ${createResp.responseCode}"
        return response
    }

    // Extract vendor ID and return
    val vendorId = extractVendorId(createResp.content)
    response.response = CsvLinkBridgeObject().apply {
        id = vendorId
    }
    return response
}
```

## Pattern 2: Import with Pagination

### Legacy Pattern (AcumaticaImportHelper.java:64-120)

```java
protected ResponseData getResponseList(List<ApiCaller> apiCallerList) {
    for (ApiCaller apiCaller : apiCallerList) {
        ResponseData result = authenticatedApiCall(request, apiCallerFactory, apiCaller::call);

        if (!result.isSuccessful()) {
            handleErrorResponse(apiCaller, result);
            break;
        }

        responseDataList.add(result);

        if (endpoint.isPaginationEnabled() && shouldPaginate(result)) {
            paginateQuery(apiCaller, result);
        }
    }
}

private void paginateQuery(ApiCaller apiCaller, ResponseData initialResponse) {
    int pageNumber = 2;
    int maxPageLimit = isNonDeltaImport ? 5000 : 100;

    while (pageNumber <= maxPageLimit) {
        AcumaticaUrlSuffixAssembler urlSuffix = apiCaller.getUrlSuffix();
        urlSuffix.addSkip((pageNumber - 1) * TOP_RESULTS); // TOP_RESULTS = 500

        ApiCaller paginatedCaller = apiCallerFactory.createRestApiCaller(
            request, apiCaller.getEndpoint(), urlSuffix
        );

        ResponseData response = authenticatedApiCall(request, apiCallerFactory, paginatedCaller::call);

        if (!response.isSuccessful() || !shouldPaginate(response)) {
            break;
        }

        responseDataList.add(response);
        pageNumber++;
    }
}
```

### Kotlin Pattern (Copy This)

```kotlin
fun getVendors(request: GetVendorsRequest): GetVendorsResponse {
    val responseDataList = mutableListOf<ResponseData>()
    val TOP_RESULTS = 500
    val maxPageLimit = if (request.isNonDeltaImport) 5000 else 100

    // Initial request
    val apiCaller = apiCallerFactory.createRestApiCaller(
        request, AcumaticaEndpoint.VENDOR, AcumaticaUrlSuffixAssembler()
    )

    val initialResponse = AcumaticaAuthenticator.authenticatedApiCall(
        request, apiCallerFactory
    ) { apiCaller.call() }

    if (!initialResponse.isSuccessful) {
        val response = GetVendorsResponse()
        response.error = "Failed to get vendors: ${initialResponse.responseCode}"
        return response
    }

    responseDataList.add(initialResponse)

    // Pagination
    var pageNumber = 2
    while (pageNumber <= maxPageLimit && shouldPaginate(initialResponse)) {
        val urlSuffix = AcumaticaUrlSuffixAssembler().apply {
            addSkip((pageNumber - 1) * TOP_RESULTS)
        }

        val paginatedCaller = apiCallerFactory.createRestApiCaller(
            request, AcumaticaEndpoint.VENDOR, urlSuffix
        )

        val response = AcumaticaAuthenticator.authenticatedApiCall(
            request, apiCallerFactory
        ) { paginatedCaller.call() }

        if (!response.isSuccessful || !shouldPaginate(response)) {
            break
        }

        responseDataList.add(response)
        pageNumber++
    }

    // Assemble response
    return assembleVendorsResponse(responseDataList)
}
```

## Pattern 3: JSON Payload Building

### Legacy Pattern (VendorPayloadMapper.java:11-80)

```java
public static String mapToAcumaticaJson(Map<String, String> raw, String vendorName, String stampliLink) {
    JSONObject json = new JSONObject();

    // Core fields with value wrapper
    json.put("VendorName", obj(vendorName));
    json.put("VendorID", obj(raw.get("vendorId")));
    json.put("note", obj("Stampli Link: " + stampliLink)); // Note the prefix!
    json.put("Status", obj("Active"));

    // Optional fields
    if (StringUtils.isNotBlank(raw.get("paymentInstructions"))) {
        JSONObject mainContact = new JSONObject();
        mainContact.put("Attention", obj(raw.get("paymentInstructions")));
        json.put("MainContact", mainContact);
    }

    // Location/remittance
    JSONObject remittanceContact = new JSONObject();
    remittanceContact.put("Email", obj(raw.get("payeeEmail")));
    JSONObject locationDetails = new JSONObject();
    locationDetails.put("RemittanceContact", remittanceContact);
    json.put("Locations", new JSONArray().put(locationDetails));

    return json.toString(2);
}

private static JSONObject obj(String value) {
    if (value == null) return null;
    return new JSONObject().put("value", value);
}
```

### Kotlin Pattern (Copy This)

```kotlin
fun mapToAcumaticaJson(raw: Map<String, String>, vendorName: String, stampliLink: String): String {
    val json = JSONObject()

    // Core fields with value wrapper - EXACT SAME STRUCTURE
    json.put("VendorName", obj(vendorName))
    json.put("VendorID", obj(raw["vendorId"]))
    json.put("note", obj("Stampli Link: $stampliLink")) // Note the prefix!
    json.put("Status", obj("Active"))

    // Optional fields
    raw["paymentInstructions"]?.takeIf { it.isNotBlank() }?.let {
        val mainContact = JSONObject()
        mainContact.put("Attention", obj(it))
        json.put("MainContact", mainContact)
    }

    // Location/remittance
    val remittanceContact = JSONObject()
    remittanceContact.put("Email", obj(raw["payeeEmail"]))
    val locationDetails = JSONObject()
    locationDetails.put("RemittanceContact", remittanceContact)
    json.put("Locations", JSONArray().put(locationDetails))

    return json.toString(2)
}

private fun obj(value: String?): JSONObject? {
    return value?.let { JSONObject().put("value", it) }
}
```

## Pattern 4: Risk Control (Duplicate Check)

### Legacy Pattern (AcumaticaDriver.java:412-470)

```java
public CsvLinkBridgeObject getMatchingVendorByStampliLink(RetrieveVendorByLinkRequest request) {
    String stampliLink = request.getLink();
    String requestedVendorId = request.getVendorId();

    AcumaticaVendorMatcher.VendorMatch match = AcumaticaVendorMatcher.findVendorMatch(
        apiCallerFactory, request, stampliLink, requestedVendorId
    );

    if (match != null && !StringUtils.equalsIgnoreCase(stampliLink, match.normalizedNote)) {
        // Different Stampli link found - conflict!
        CsvLinkBridgeObject cl = new CsvLinkBridgeObject();
        cl.setErrorCode(ExportErrorCodeBridgeObject.EXCEPTION);
        cl.setError(String.format(
            "Vendor already exists in Acumatica with a different Stampli link (expected: %s, found: %s)",
            stampliLink, match.normalizedNote
        ));
        return cl;
    }

    if (match != null) {
        // Same link - idempotent success
        CsvLinkBridgeObject cl = new CsvLinkBridgeObject();
        cl.setId(match.vendorId);
        return cl;
    }

    // No match found
    return null;
}
```

### Kotlin Pattern (Copy This)

```kotlin
fun checkDuplicateVendor(request: ExportVendorRequest, stampliLink: String): DuplicateCheckResult {
    // Search for existing vendor
    val match = findVendorByLink(request, stampliLink)

    return when {
        // Different link - error
        match != null && !match.normalizedNote.equals(stampliLink, ignoreCase = true) -> {
            DuplicateCheckResult.LinkMismatch(
                "Vendor already exists in Acumatica with a different Stampli link (expected: $stampliLink, found: ${match.normalizedNote})"
            )
        }

        // Same link - idempotent
        match != null -> {
            DuplicateCheckResult.Exists(match.vendorId)
        }

        // No match
        else -> DuplicateCheckResult.NotFound
    }
}

sealed class DuplicateCheckResult {
    object NotFound : DuplicateCheckResult()
    data class Exists(val vendorId: String) : DuplicateCheckResult()
    data class LinkMismatch(val error: String) : DuplicateCheckResult()
}
```

## Pattern 5: Test Setup

### Legacy Pattern (AcumaticaDriverITest.java:20-45)

```java
private static final String BASE_URL = "http://63.32.187.185/StampliAcumaticaDB";
private static final String USERNAME = "admin";
private static final String PASSWORD = "Password1";
private static final String SUBSIDIARY = "StampliCompany";

private AcumaticaDriver driver;
private Map<String, String> connectionProperties;

@Before
public void setUp() {
    driver = new AcumaticaDriver();
    connectionProperties = new HashMap<>();
    connectionProperties.put("hostname", BASE_URL);
    connectionProperties.put("user", USERNAME);
    connectionProperties.put("password", PASSWORD);
}
```

### Kotlin Pattern (Copy This)

```kotlin
class KotlinAcumaticaDriverTest {
    companion object {
        const val BASE_URL = "http://63.32.187.185/StampliAcumaticaDB"
        const val USERNAME = "admin"
        const val PASSWORD = "Password1"
        const val SUBSIDIARY = "StampliCompany"
    }

    private lateinit var driver: KotlinAcumaticaDriver
    private lateinit var connectionProperties: Map<String, String>

    @BeforeEach
    fun setUp() {
        driver = KotlinAcumaticaDriver()
        connectionProperties = mapOf(
            "hostname" to BASE_URL,
            "user" to USERNAME,
            "password" to PASSWORD
        )
    }

    private fun createRequest(vendorName: String = "Test Vendor"): ExportVendorRequest {
        return ExportVendorRequest().apply {
            subsidiary = SUBSIDIARY
            dualDriverName = "com.stampli.kotlin.driver.KotlinAcumaticaDriver"
            this.connectionProperties = this@KotlinAcumaticaDriverTest.connectionProperties
            finSysBridgeTransferredObject = FinSysBridgeTransferredObject()
            rawData = mapOf("vendorName" to vendorName)
        }
    }
}
```

## Critical Patterns to Follow

1. **Validation Order**: Check required fields first, then length limits
2. **Error Messages**: Use EXACT messages from legacy (case-sensitive)
3. **JSON Structure**: Acumatica expects `{"field": {"value": "data"}}`
4. **Stampli Link**: Always prefix with "Stampli Link: " in note field
5. **Authentication**: Always wrap with `AcumaticaAuthenticator.authenticatedApiCall`
6. **Pagination**: 500 items per page, different limits for delta vs full
7. **Response Building**: Set error OR response, never both
8. **Test Data**: Use timestamps to avoid conflicts

## Files to Scan for Patterns

| Pattern | File | Lines | Purpose |
|---------|------|-------|---------|
| Export | CreateVendorHandler.java | 22-90 | Validation and API call |
| Import | AcumaticaImportHelper.java | 64-120 | Pagination |
| Mapping | VendorPayloadMapper.java | 11-80 | JSON building |
| Risk Control | AcumaticaDriver.java | 412-470 | Duplicate check |
| Authentication | AcumaticaAuthenticator.java | All | Session management |
| Test | AcumaticaDriverITest.java | 20-45 | Setup pattern |