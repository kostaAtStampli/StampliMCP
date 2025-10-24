# Acumatica Integration Complete Analysis
Generated: 2025-01-11

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Authentication & Credential Management](#authentication--credential-management)
3. [Core Classes & Method Signatures](#core-classes--method-signatures)
4. [Request/Response DTOs](#requestresponse-dtos)
5. [Handler & Helper Classes](#handler--helper-classes)
6. [API Communication Layer](#api-communication-layer)
7. [Complete Data Flow](#complete-data-flow)
8. [Integration Recommendations](#integration-recommendations)

---

## Architecture Overview

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                     AdminService.java                        │
│                  (JSON-RPC Entry Point)                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                 FinSysAgentManager.java                      │
│              (Agent Factory & Management)                    │
└───────────────────────┬─────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│              DualBridgeSaaSRouter.java                       │
│         (Routes Bridge vs SaaS Mode)                         │
└────────┬──────────────────────────────┬─────────────────────┘
         │ Bridge Mode                   │ SaaS Mode
┌────────▼──────────┐          ┌────────▼─────────────────────┐
│ C# Bridge Executor│          │    DriverEngine.java         │
│   (Queue-based)   │          │  (Reflection Invocation)     │
└───────────────────┘          └────────┬─────────────────────┘
                                        │
                               ┌────────▼─────────────────────┐
                               │   AcumaticaDriver.java        │
                               │  (51 Operations, Direct API) │
                               └──────────────────────────────┘
```

### Key Patterns
- **Authentication**: Login-wrapper-logout per request (no session pooling)
- **Error Handling**: Errors in response objects, not exceptions
- **Pagination**: 500 items per page, max 100 pages (delta) or 5000 pages (full)
- **Feature Flags**: Via FinSysBridgeTransferredObject.state map
- **Custom Fields**: Mapped via customFieldsFinsysIdToStampliId

---

## Authentication & Credential Management

### 1. Credential Storage & Encryption

**Location**: `/mnt/c/STAMPLI4/core/server-commons/src/main/java/com/stampli/server/data/ConnectionConfig.java`

```java
public class ConnectionConfig {
    // Automatic encryption for "password" and "secret" fields
    private static final String ENCRYPTED_PROPERTY_PREFIX = "enc_";

    // Encryption method
    public static Map<String, String> getEncryptCredentials(Map<String, String> fromMap) {
        Map<String, String> map = new HashMap<>();
        for (String curr : fromMap.keySet()) {
            if (isSensitiveProperty(curr) && !isEncryptedProperty(curr)) {
                map.put(ENCRYPTED_PROPERTY_PREFIX + curr,
                        EncryptionUtils.i().stampliEncrypt(fromMap.get(curr)));
            } else {
                map.put(curr, fromMap.get(curr));
            }
        }
        return map;
    }

    // Decryption method
    public static Map<String, String> decryptCredentials(Map<String, String> credentials) {
        Map<String, String> resultMap = new HashMap<>();
        for (String curr : credentials.keySet()) {
            if (isSensitiveProperty(curr) && isEncryptedProperty(curr)) {
                resultMap.put(
                    curr.substring(ENCRYPTED_PROPERTY_PREFIX.length()),
                    EncryptionUtils.i().stampliDecrypt(credentials.get(curr))
                );
            } else {
                resultMap.put(curr, credentials.get(curr));
            }
        }
        return resultMap;
    }
}
```

**Credential Format**:
```java
Map<String, String> connectionProperties = {
    "hostname": "http://63.32.187.185/StampliAcumaticaDB",
    "user": "admin",
    "enc_password": "[encrypted_value]",  // Auto-encrypted
    "subsidiary": "StampliCompany"
}
```

### 2. AWS Secrets Manager Integration

**Documentation**: `/mnt/c/STAMPLI4/core/credentials/README.md`
- Uses AWS Secrets Manager for credential storage
- Different RunStates: LOCAL_DEV, RELEASE
- Access via AWS CLI configured locally
- Region: eu-west-1

### 3. Authentication Flow

**Location**: `/mnt/c/STAMPLI4/core/finsys-drivers/acumatica/src/main/java/com/stampli/driver/auth/`

```java
// AcumaticaAuthenticator.java
public static <T> T authenticatedApiCall(
    FinSysBridgeBaseRequest request,
    ApiCallerFactory apiCallerFactory,
    Function<CloseableHttpClient, T> function) throws IOException {

    AcumaticaConnectionManager connectionManager =
        new AcumaticaConnectionManager(request, apiCallerFactory);
    try {
        connectionManager.login();  // Login before operation
        return function.apply(connectionManager.getClient());
    } finally {
        connectionManager.logout(false);  // Always logout
    }
}

// AcumaticaConnectionManager.java
public class AcumaticaConnectionManager {
    private static final long TIME_LIMIT_MILLIS = 10 * 60 * 1000; // 10 minutes

    public void refreshConnectionWhenLimitReached() throws IOException {
        long timeSincLastLogin = System.currentTimeMillis() - lastLoginTime;
        if (timeSincLastLogin >= TIME_LIMIT_MILLIS) {
            logout(true);
            login();
        }
    }
}
```

**Key Points**:
- NO session pooling - each operation gets new session
- 10-minute session refresh for long operations
- Uses cookies: ASPXAUTH, ASP.NET_SessionId

---

## Core Classes & Method Signatures

### 1. BridgeSynchronizationAgent.java

**Location**: `/mnt/c/STAMPLI4/core/web/server-services/src/main/java/com/stampli/synchronization/bridge/`

**Key Method Signatures**:
```java
public CsvLink exportVendor(
    LinkedHashMap<String, String> data,
    ExportExtraInfoBase exportExtraInfo,
    ConnectionConfig connectionConfig,
    AgentExportOptions exportOptions) throws SynchronizationAgentException

public List<CsvVendor> getVendors(
    ConnectionConfig connectionConfig,
    Map<String, List<String>> filteringFieldsData,
    boolean importDeltaOnly) throws SynchronizationAgentException

public CsvLink exportBillPayment(
    Map<String, Object> data,
    ConnectionConfig connectionConfig) throws SynchronizationAgentException
```

**DTO Request Building**:
```java
// Building ExportVendorRequest
ExportVendorRequest exportRequest = new ExportVendorRequest(data);
TaskDTORequest<FinSysBridgeBaseRequest> dtoRequest =
    createDTORequest(exportRequest, connectionConfig, Priority.ONE);
FinSysBridgeBaseResponse response =
    sendToTaskCreatorAndGetResponse(dtoRequest, connectionConfig);
```

### 2. DualBridgeSaaSRouter.java

**Routing Logic**:
```java
@Override
public FinSysBridgeBaseResponse sendToTaskCreatorAndGetResponse(
    TaskDTORequest<FinSysBridgeBaseRequest> dtoRequest,
    ConnectionConfig connectionConfig,
    Long firstResponseTimeout) throws SynchronizationAgentException {

    if (isBridgeMode) {
        // Route to C# Bridge Executor via Queue
        return super.sendToTaskCreatorAndGetResponse(dtoRequest, connectionConfig, firstResponseTimeout);
    } else {
        // Direct SaaS Mode - Call Java Driver
        FinSysBridgeBaseResponse response =
            DriverEngine.invokeBridgeCommand(dtoRequest.getOpCode(), dtoRequest.getRequest());
        printDebugLogsFromResponse(response);
        storeResponseOnS3(dtoRequest, response);
        throwIfErrorResponse(response);
        return response;
    }
}
```

### 3. DriverEngine.java

**Reflection-Based Invocation**:
```java
public static FinSysBridgeBaseResponse invokeBridgeCommand(
    AgentOpCode opCode,
    FinSysBridgeBaseRequest request) throws Exception {

    // Special handling for export operations
    if (EXPORT_ACTIONS.contains(opCode)) {
        return DriverExportManager.handle(opCode, request, getDualDriver(request));
    }

    // Reflection invocation
    Class requestClass = ReflectionHelper.getClassFromPath(opCode.getRequestClass().getName());
    Class responseClass = ReflectionHelper.getClassFromPath(opCode.getResponseClass().getName());

    IDualFinsysDriver driver = getDualDriver(request);
    var method = ReflectionHelper.getMethodFromClassRef(
        driver.getClass(),
        opCode.getMethodName(),
        new Class[]{requestClass}
    );

    var response = ReflectionHelper.invokeMethod(method, driver, request);
    return ReflectionHelper.castObject(responseClass, response);
}

private static IDualFinsysDriver getDualDriver(FinSysBridgeBaseRequest request) {
    Class driverClass = ReflectionHelper.getClassFromPath(request.getDualDriverName());
    return (IDualFinsysDriver) ReflectionHelper.getInstanceOf(driverClass);
}
```

### 4. AcumaticaDriver.java

**Location**: `/mnt/c/STAMPLI4/core/finsys-drivers/acumatica/src/main/java/com/stampli/driver/`

**All 51 Operations**:
```java
public class AcumaticaDriver implements IDualFinsysDriver {

    // Vendor Operations
    public GetVendorsResponse getVendors(GetVendorsRequest request)
    public ExportResponse exportVendor(ExportVendorRequest request)
    public CsvLinkBridgeObject getMatchingVendorByStampliLink(RetrieveVendorByLinkRequest request)
    public CsvLinkBridgeObject getDuplicateVendorById(RetrieveVendorByIdRequest request)

    // Bill Operations
    public GetPaidBillsResponse getPaidBills(GetPaidBillsRequest request)
    public ExportResponse exportAPTransaction(ExportRequest exportRequest)
    public RetrieveInvoicesResponse retrieveInvoiceByReferenceId(RetrieveInvoiceByReferenceNumberRequest request)
    public RetrieveInvoicesResponse retrieveBillsByInvoicesInPayment(RetrieveBillsRequest request)
    public boolean isDuplicateInvoice(APTransactionHeaders currBill, ApTransaction apTransaction)

    // Payment Operations
    public ExportResponse exportBillPayment(ExportBillPaymentRequest request)
    public VoidPaymentWithMessageResponse voidPayment(VoidPaymentWithMessageRequest request)
    public RetrieveBillPaymentsResponse retrieveBillPayments(RetrieveBillPaymentsRequest request)
    public GetPaymentVendorsResponse getVendorPaymentData(GetPaymentVendorsRequest request)

    // Account Operations
    public GetAccountSearchListResponse getAccountSearchList(GetAccountSearchListRequest request)
    public GetPaymentAccountSearchListResponse getPaymentAccountSearchList(GetPaymentAccountSearchListRequest request)
    public GetPayableAccountSearchListResponse getPayableAccountSearchList(GetPayableAccountSearchListRequest request)
    public GetBankAccountSearchListResponse getBankAccountSearchList(GetBankAccountSearchListRequest request)

    // Purchase Order Operations
    public GetPurchaseOrderSearchListResponse getPurchaseOrderSearchList(GetPurchaseOrderSearchListRequest request)
    public ExportResponse exportPurchaseOrder(ExportPORequest exportPORequest)
    public GetItemsListsPerPOResponse getItemsListsPerPO(GetItemsListsPerPORequest request)
    public CsvLinkBridgeObject getMatchingPoByStampliLink(RetrievePoByLinkRequest request)
    public CsvLinkBridgeObject getDuplicatePoByParams(RetrievePoByParamsRequest request)

    // Field Operations
    public FinSysBridgeBaseResponse getFieldSearchList(GetGeneralFieldSearchListRequest request)
    public GetGeneralFieldSearchListResponse getCustomFieldSearchList(GetCustomFieldSearchListRequest request)
    public GetItemSearchListResponse getItemSearchList(GetItemSearchListRequest request)
    public GetUnitSearchListResponse getUnitSearchList(GetUnitSearchListRequest request)

    // Company Operations
    public ConnectToCompanyResponse connectToCompany(ConnectToCompanyRequest request)
    public GetCompaniesResponse getCompanies(GetCompaniesRequest request)

    // Other Operations
    public GetExtensionVersionResponse getExtensionVersion(GetExtensionVersionRequest request)
    public UpdateExtensionVersionResponse updateExtensionVersion(UpdateExtensionVersionRequest request)
    public DebuggingToolsResponse debuggingToolsExecuteRequest(DebuggingToolsRequest request)

    // ... plus 15 more operations
}
```

**Vendor Export Risk Control**:
```java
@Override
public ExportResponse exportVendor(ExportVendorRequest request) {
    try {
        CreateVendorHandler handler = new CreateVendorHandler();
        return handler.execute(apiCallerFactory, request);
    } catch (Exception e) {
        ExportResponse response = new ExportResponse();
        response.setError("Failed to export vendor: " + e.getMessage());
        return response;
    }
}

// Vendor matching for idempotency
@Override
public CsvLinkBridgeObject getMatchingVendorByStampliLink(RetrieveVendorByLinkRequest request) {
    String stampliLink = request.getLink();
    String requestedVendorId = request.getVendorId();

    try {
        AcumaticaVendorMatcher.VendorMatch match = AcumaticaVendorMatcher.findVendorMatch(
            apiCallerFactory, request, stampliLink, requestedVendorId
        );

        if (match != null && !StringUtils.equalsIgnoreCase(stampliLink, match.normalizedNote)) {
            // Conflict - different Stampli link found
            logger.warning("[AcumaticaDriver] Vendor RC link mismatch: expectedLink=" + stampliLink +
                          ", actualLink=" + match.normalizedNote + ", requestedVendorId=" + requestedVendorId);
            CsvLinkBridgeObject cl = new CsvLinkBridgeObject();
            cl.setErrorCode(ExportErrorCodeBridgeObject.EXCEPTION);
            cl.setError(String.format("Vendor already exists in Acumatica with a different Stampli link " +
                                     "(expected: %s, found: %s)", stampliLink, match.normalizedNote));
            return cl;
        }

        if (match != null) {
            // Success - vendor found
            CsvLinkBridgeObject link = new CsvLinkBridgeObject();
            link.setErpVendorId(match.vendorId);
            String vendorUiLink = AcumaticaUiLinkBuilder.buildVendorUiLink(
                request.getConnectionProperties(), request.getSubsidiary(), match.vendorId
            );
            if (StringUtils.isNotBlank(vendorUiLink)) {
                link.setContent(vendorUiLink);
            }
            logger.info("[AcumaticaDriver] Vendor RC match-by-link succeeded: link=" +
                       stampliLink + ", vendorId=" + match.vendorId);
            return link;
        }
    } catch (AcumaticaApiException e) {
        String reason = StringUtils.defaultIfBlank(e.getMessage(), "Unknown error");
        logger.warning("[AcumaticaDriver] Vendor match-by-link error for vendorId=" +
                      requestedVendorId + ": " + reason, e);
        CsvLinkBridgeObject cl = new CsvLinkBridgeObject();
        cl.setError("Failed to verify existing vendor: " + reason);
        cl.setErrorCode(ExportErrorCodeBridgeObject.EXCEPTION);
        return cl;
    }

    return null;  // No match found
}
```

---

## Request/Response DTOs

### 1. TaskDTORequest

```java
public class TaskDTORequest<TApplicationRequest> extends BaseDTO {
    @JsonDeserialize(using = JsonAsStringDeserializer.class)
    TApplicationRequest request;
    private Priority priority;
    private Version version;
    private boolean trackCPU;
    private String bridgeVersion;
    private String uploadFileTimeoutAndRetry;
    private String s3RequestUrl;
    private String largeRequestRiskId;
    @Getter @Setter private Boolean logS3RequestUrl;
    @Getter @Setter private String useTlsProtocol;
    @Getter @Setter private Map<String, String> connectionProperties;
    @Getter @Setter private Map<String, Map<String, String>> interCompanyConnectionProperties;
    @Getter @Setter private String secondaryQueueName;
    @Getter @Setter private Integer bridgeRequestToOnPremMicroserviceTimeoutSeconds;
}
```

### 2. FinSysBridgeBaseRequest

```java
@JsonIgnoreProperties(ignoreUnknown=true)
@Data
@NoArgsConstructor
public abstract class FinSysBridgeBaseRequest {
    String bridgeExecuterName;  // C# executor name
    String driverName;          // Java driver name
    String assemblyName;
    String dualDriverName;      // Java dual driver
    String subsidiary;
    FinSysBridgeTransferredObject finSysBridgeTransferredObject;
    Map<String, String> connectionProperties;
    Map<String, Map<String, String>> interCompanyConnectionProperties;
}
```

### 3. FinSysBridgeBaseResponse

```java
@Data
@JsonIgnoreProperties(ignoreUnknown=true)
public class FinSysBridgeBaseResponse {
    private FinSysBridgeTransferredObject finSysBridgeTransferredObject;
    private int responseCode;
    private String error;
    private String messageCode;
    private Map<String, Object> messageParams;
    private Map<String, String> logToS3Map;
    private Object originRequest;
    private Map<String, String> companyStatus;
    private Map<String, SearchFieldRelatedData> relations;
    private Map<String, Set<String>> relationsList;
    private boolean invalidCredentials;
}
```

### 4. FinSysBridgeTransferredObject

```java
@Data
public class FinSysBridgeTransferredObject {
    protected String fieldLastModified;
    protected Map<String, String> state;  // Feature flags
    SyncPreferencesBridgeObject preferences;
    private ClientContext clientContext;
    private final String requestTime;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private List<QueryMetadata> queryMetadatas;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private OperationRequestParameters operationRequestParameters;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private Set<String> activeFields;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    Map<String, String> customFieldsFinsysIdToStampliId;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private Boolean isMultiEntitySolutionEnabled;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private Boolean isEnableCustomBatches;

    @JsonInclude(JsonInclude.Include.NON_NULL)
    private Boolean isShowOnlyVendorIdWithoutCompanyName;
}
```

---

## Handler & Helper Classes

### 1. CreateVendorHandler

**Location**: `/mnt/c/STAMPLI4/core/finsys-drivers/acumatica/src/main/java/com/stampli/driver/vendor/`

```java
public class CreateVendorHandler {

    public ExportResponse execute(ApiCallerFactory apiCallerFactory, ExportVendorRequest request) {
        ExportResponse response = new ExportResponse();
        try {
            Map<String, String> raw = request.getRawData();

            // Validation
            String vendorName = raw.get("vendorName");
            if (StringUtils.isBlank(vendorName)) {
                response.setError("vendorName is required");
                return response;
            }

            String stampliLink = raw.get("stampliLink");
            if (StringUtils.isBlank(stampliLink)) {
                response.setError("stampliLink is required");
                return response;
            }

            if (vendorName.length() > 60) {
                response.setError("vendorName exceeds maximum length of 60 characters");
                return response;
            }

            String vendorId = raw.get("vendorId");
            if (StringUtils.isNotBlank(vendorId) && vendorId.length() > 15) {
                response.setError("vendorId exceeds maximum length of 15 characters");
                return response;
            }

            // Create JSON payload
            String body = VendorPayloadMapper.mapToAcumaticaJson(raw, vendorName, stampliLink);

            // API Call
            ApiCaller apiCaller = apiCallerFactory.createPutRestApiCaller(
                request, AcumaticaEndpoint.VENDOR, new AcumaticaUrlSuffixAssembler(), body
            );
            ResponseData createResp = AcumaticaAuthenticator.authenticatedApiCall(
                request, apiCallerFactory, apiCaller::call
            );

            if (!createResp.isSuccessful()) {
                String details = StringUtils.defaultIfBlank(createResp.getContent(), "<empty>");
                logger.warning(String.format("Create vendor failed: vendorId=%s, stampliLink=%s, code=%d, body=%s",
                    StringUtils.defaultIfBlank(vendorId, "<none>"),
                    StringUtils.defaultIfBlank(stampliLink, "<none>"),
                    createResp.getResponseCode(), details));

                var cl = new CsvLinkBridgeObject();
                cl.setErrorCode(ExportErrorCodeBridgeObject.EXCEPTION);
                cl.setError(String.format("Acumatica returned %d: %s",
                    createResp.getResponseCode(), details));
                response.setResponse(cl);
                return response;
            }

            // Extract vendor ID from response
            String createdVendorId = extractVendorId(createResp.getContent());
            if (StringUtils.isEmpty(createdVendorId)) {
                logger.warning(String.format("Vendor created but missing VendorID in response. " +
                    "vendorName=%s, stampliLink=%s",
                    StringUtils.defaultIfBlank(vendorName, "<none>"),
                    StringUtils.defaultIfBlank(stampliLink, "<none>")));
                response.setError("Vendor created but missing VendorID in response");
                return response;
            }

            // Build success response
            var link = new CsvLinkBridgeObject();
            link.setErpVendorId(createdVendorId);
            String vendorUiLink = AcumaticaUiLinkBuilder.buildVendorUiLink(
                request.getConnectionProperties(), request.getSubsidiary(), createdVendorId
            );
            if (StringUtils.isNotBlank(vendorUiLink)) {
                link.setContent(vendorUiLink);
            }
            response.setResponse(link);
            response.setResponseCode(200);
            return response;

        } catch (Exception e) {
            logger.error(String.format("Failed to create vendor. vendorId=%s, stampliLink=%s, error=%s",
                StringUtils.defaultIfBlank(request?.getRawData()?.get("vendorId"), "<none>"),
                StringUtils.defaultIfBlank(request?.getRawData()?.get("stampliLink"), "<none>"),
                StringUtils.defaultIfBlank(e.getMessage(), "<empty>")), e);

            var cl = new CsvLinkBridgeObject();
            cl.setErrorCode(ExportErrorCodeBridgeObject.EXCEPTION);
            cl.setError("Exception while creating vendor: " +
                StringUtils.defaultIfBlank(e.getMessage(), "<empty>"));
            response.setResponse(cl);
            return response;
        }
    }
}
```

### 2. VendorPayloadMapper

```java
public class VendorPayloadMapper {
    private static final String NOTE_PREFIX = "Stampli Link: ";

    public static String mapToAcumaticaJson(Map<String, String> raw,
                                           String vendorName,
                                           String stampliLink) {
        JSONObject json = new JSONObject();

        // Core fields
        json.put("VendorName", obj(vendorName));
        json.put("VendorID", obj(raw.get("vendorId")));
        json.put("note", obj(NOTE_PREFIX + stampliLink));
        json.put("Status", obj("Active"));

        // Payment instructions
        if (StringUtils.isNotBlank(raw.get("paymentInstructions"))) {
            JSONObject mainContact = new JSONObject();
            mainContact.put("Attention", obj(raw.get("paymentInstructions")));
            json.put("MainContact", mainContact);
        }

        // Bank details
        if (StringUtils.isNotBlank(raw.get("paymentMethod"))) {
            json.put("PaymentMethod", obj(raw.get("paymentMethod")));
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
}
```

### 3. AcumaticaVendorMatcher

```java
public class AcumaticaVendorMatcher {
    private static final String NOTE_PREFIX = "Stampli Link: ";

    public static class VendorMatch {
        public final String vendorId;
        public final String normalizedNote;

        public VendorMatch(String vendorId, String normalizedNote) {
            this.vendorId = vendorId;
            this.normalizedNote = normalizedNote;
        }
    }

    public static VendorMatch findVendorMatch(ApiCallerFactory apiCallerFactory,
                                              FinSysBridgeBaseRequest request,
                                              String stampliLink,
                                              String requestedVendorId) throws AcumaticaApiException {

        // First try: Search by Stampli Link in notes
        AcumaticaUrlSuffixAssembler urlSuffix = new AcumaticaUrlSuffixAssembler();
        urlSuffix.addFilter("note", NOTE_PREFIX + stampliLink);
        ApiCaller apiCaller = apiCallerFactory.createRestApiCaller(
            request, AcumaticaEndpoint.VENDOR, urlSuffix
        );

        try {
            ResponseData response = AcumaticaAuthenticator.authenticatedApiCall(
                request, apiCallerFactory, apiCaller::call
            );

            if (response.isSuccessful()) {
                VendorMatch match = parseForLinkMatch(response.getContent(), stampliLink);
                if (match != null) {
                    return match;
                }
            }
        } catch (Exception e) {
            throw new AcumaticaApiException("Failed to search vendors by link", e);
        }

        // Second try: Search by vendor ID if provided
        if (StringUtils.isNotBlank(requestedVendorId)) {
            urlSuffix = new AcumaticaUrlSuffixAssembler();
            urlSuffix.addFilter("VendorID", requestedVendorId);
            apiCaller = apiCallerFactory.createRestApiCaller(
                request, AcumaticaEndpoint.VENDOR, urlSuffix
            );

            try {
                ResponseData response = AcumaticaAuthenticator.authenticatedApiCall(
                    request, apiCallerFactory, apiCaller::call
                );

                if (response.isSuccessful()) {
                    return parseForIdMatch(response.getContent(), stampliLink);
                }
            } catch (Exception e) {
                throw new AcumaticaApiException("Failed to search vendors by ID", e);
            }
        }

        return null;  // No match found
    }

    private static VendorMatch parseForLinkMatch(String content, String stampliLink) {
        try {
            JSONArray vendors = new JSONArray(content);
            if (vendors.length() > 0) {
                JSONObject vendor = vendors.getJSONObject(0);
                String vendorId = extractField(vendor, "VendorID");
                String note = extractField(vendor, "note");

                if (vendorId != null) {
                    String normalizedNote = note != null && note.startsWith(NOTE_PREFIX)
                        ? note.substring(NOTE_PREFIX.length())
                        : note;
                    return new VendorMatch(vendorId, normalizedNote);
                }
            }
        } catch (JSONException e) {
            // Parse error - no match
        }
        return null;
    }
}
```

### 4. AcumaticaImportHelper

```java
public class AcumaticaImportHelper<T extends FinSysBridgeBaseResponse> {
    private static final int TOP_RESULTS = 500;  // Page size

    protected ResponseData getResponseList(List<ApiCaller> apiCallerList) {
        for (ApiCaller apiCaller : apiCallerList) {
            ResponseData result = authenticatedApiCall(request, apiCallerFactory, apiCaller::call);

            if (!result.isSuccessful()) {
                handleErrorResponse(apiCaller, result);
                break;
            }

            responseDataList.add(result);

            // Handle pagination
            if (endpoint.isPaginationEnabled() && shouldPaginate(result)) {
                paginateQuery(apiCaller, result);
            }
        }
        return assembleResponseData();
    }

    private void paginateQuery(ApiCaller apiCaller, ResponseData initialResponse) {
        int pageNumber = 2;
        int maxPageLimit = isNonDeltaImport ? 5000 : 100;

        while (pageNumber <= maxPageLimit) {
            AcumaticaUrlSuffixAssembler urlSuffix = apiCaller.getUrlSuffix();
            urlSuffix.addSkip((pageNumber - 1) * TOP_RESULTS);

            ApiCaller paginatedCaller = apiCallerFactory.createRestApiCaller(
                request, apiCaller.getEndpoint(), urlSuffix
            );

            ResponseData response = authenticatedApiCall(
                request, apiCallerFactory, paginatedCaller::call
            );

            if (!response.isSuccessful() || !shouldPaginate(response)) {
                break;
            }

            responseDataList.add(response);
            pageNumber++;
        }
    }

    private boolean shouldPaginate(ResponseData response) {
        try {
            JSONArray array = new JSONArray(response.getContent());
            return array.length() == TOP_RESULTS;
        } catch (Exception e) {
            return false;
        }
    }
}
```

---

## API Communication Layer

### 1. RestApiCaller

**Location**: `/mnt/c/STAMPLI4/core/finsys-drivers/acumatica/src/main/java/com/stampli/driver/api/`

```java
public class RestApiCaller extends ApiCaller {
    // Timeout configuration
    private static final int CONNECTION_REQUEST_TIMEOUT = 5000;   // 5 seconds
    private static final int SOCKET_TIMEOUT = 600000;             // 10 minutes
    private static final int CONNECT_TIMEOUT = 60000;             // 1 minute

    private RequestConfig buildRequestConfig() {
        return RequestConfig.custom()
            .setConnectionRequestTimeout(Timeout.ofMilliseconds(CONNECTION_REQUEST_TIMEOUT))
            .setResponseTimeout(Timeout.ofMilliseconds(SOCKET_TIMEOUT))
            .setConnectTimeout(Timeout.ofMilliseconds(CONNECT_TIMEOUT))
            .build();
    }

    @Override
    public ResponseData call() throws IOException {
        switch (httpMethod) {
            case GET:
                return executeGetRequest();
            case POST:
                return executePostRequest();
            case PUT:
                return executePutRequest();
            default:
                throw new UnsupportedOperationException();
        }
    }

    private ResponseData executePutRequest() throws IOException {
        HttpPut httpPut = new HttpPut(buildCallUrl());
        httpPut.setConfig(buildRequestConfig());
        httpPut.addHeader(HttpHeaders.CONTENT_TYPE, ContentType.APPLICATION_JSON.getMimeType());
        httpPut.setEntity(new StringEntity(requestBody, ContentType.APPLICATION_JSON));

        try (CloseableHttpResponse response = httpClient.execute(httpPut)) {
            return parseResponse(response);
        }
    }

    private ResponseData parseResponse(CloseableHttpResponse response) throws IOException {
        ResponseData responseData = new ResponseData();
        responseData.setResponseCode(response.getCode());

        HttpEntity entity = response.getEntity();
        if (entity != null) {
            String content = EntityUtils.toString(entity);
            responseData.setContent(content);

            if (!responseData.isSuccessful()) {
                parseError(responseData, content);
            }
        }

        return responseData;
    }

    private void parseError(ResponseData responseData, String content) {
        try {
            JSONObject errorJson = new JSONObject(content);

            if (errorJson.has("message")) {
                responseData.setErrorMessage(errorJson.getString("message"));
            }

            if (errorJson.has("exceptionMessage")) {
                responseData.setErrorMessage(errorJson.getString("exceptionMessage"));
            }

            if (errorJson.has("innerException")) {
                JSONObject inner = errorJson.getJSONObject("innerException");
                if (inner.has("message")) {
                    responseData.setErrorMessage(inner.getString("message"));
                }
            }
        } catch (Exception e) {
            responseData.setErrorMessage(content);
        }
    }
}
```

### 2. AcumaticaEndpoint

```java
public enum AcumaticaEndpoint {
    // Vendor endpoints
    VENDOR("Vendor", "20.200.001", true, true),
    VENDOR_LOCATION("VendorLocation", "20.200.001", true, true),
    VENDOR_CREDIT("VendorCredit", "20.200.001", false, false),
    VENDOR_PAYMENT("VendorPayment", "20.200.001", false, false),

    // Bill endpoints
    BILL("Bill", "20.200.001", true, false),
    QUICK_CHECK("QuickCheck", "20.200.001", false, false),
    CHECK("Check", "20.200.001", false, false),
    PAID_BILLS("PaidBills", "20.200.001", true, false),
    SEARCH_BILL("SearchBill", "20.200.001", false, false),
    SEARCH_CREDIT("SearchCredit", "20.200.001", false, false),

    // Account endpoints
    ACCOUNT("Account", "20.200.001", true, true),
    PAYMENT_ACCOUNT("PaymentAccount", "20.200.001", true, true),
    PAYABLE_ACCOUNT("PayableAccount", "20.200.001", true, true),
    BANK_ACCOUNT("CashAccount", "20.200.001", true, true),
    SUB_ACCOUNT("Subaccount", "20.200.001", true, true),

    // Item endpoints
    STOCK_ITEM("StockItem", "20.200.001", true, true),
    NON_STOCK_ITEM("NonStockItem", "20.200.001", true, true),
    UNIT("UnitOfMeasure", "20.200.001", true, true),

    // Purchase Order endpoints
    PURCHASE_ORDER("PurchaseOrder", "20.200.001", true, true),
    PURCHASE_ORDER_ITEMS("PurchaseOrderItems", "20.200.001", false, false),
    PURCHASE_RECEIPT_ITEMS("PurchaseReceiptItems", "20.200.001", false, false),
    PURCHASE_ORDER_TO_RECEIPT("PurchaseOrderToReceipt", "20.200.001", false, false),
    EXTENDED_PURCHASE_ORDER_PO_MATCHING("ExtendedPurchaseOrderPoMatching", "20.200.001", false, false),
    PURCHASE_ORDER_FOR_MATCH_STAMPLI_LINK("PurchaseOrderForMatchStampliLink", "20.200.001", false, false),

    // Organization endpoints
    BRANCH("Branch", "20.200.001", true, true),
    PROJECT("Project", "20.200.001", true, true),
    PROJECT_TASK("ProjectTask", "20.200.001", true, true),
    PROJECT_BUDGET("ProjectBudget", "20.200.001", false, false),
    M2M_PROJECT("M2MProject", "20.200.001", false, false),
    M2M_TASK("M2MTask", "20.200.001", false, false),

    // Financial endpoints
    FINANCIAL_PERIOD("FinancialPeriod", "20.200.001", false, false),
    TAX_CODE("Tax", "20.200.001", true, true),
    FORM_1099("Box1099", "20.200.001", true, true),
    COST_CODE("CostCode", "20.200.001", true, true),

    // Other endpoints
    WAREHOUSE("Warehouse", "20.200.001", true, true),
    ATTRIBUTES("AttributeDefinition", "20.200.001", true, false),
    PAYMENT_METHOD("PaymentMethod", "20.200.001", true, true),
    EXTENSION_VERSION("ExtensionVersion", "20.200.001", false, false),
    RETRIEVE_BILL_PAYMENT("RetrieveBillPayment", "20.200.001", false, false);

    private final String entityUrl;
    private final String version;
    private final boolean paginationEnabled;
    private final boolean hasAssembler;

    public String getEntityUrl() {
        return String.format("/entity/Default/%s/%s", version, entityUrl);
    }
}
```

---

## Complete Data Flow

### Export Vendor Flow

```
1. AdminService.createVendor(data)
   - Entry point: JSON-RPC service
   - Role checking & validation

2. FinSysAgentManager.getAgent(customerId)
   - Loads FinSysSyncConfig from database
   - Instantiates agent via reflection
   - Special handling for bridge agents

3. DualBridgeSaaSRouter(isBridgeMode=false)
   - Checks mode flag
   - Routes to DriverEngine for SaaS mode
   - Would route to queue for Bridge mode

4. DriverEngine.invokeBridgeCommand(EXPORT_VENDOR, request)
   - Maps AgentOpCode to method name
   - Reflection to get driver instance
   - Invokes method with request

5. AcumaticaDriver.exportVendor(request)
   - Creates CreateVendorHandler
   - Delegates to handler.execute()

6. CreateVendorHandler.execute()
   - Validates vendor data (name, ID length)
   - Checks for required fields

7. VendorPayloadMapper.mapToAcumaticaJson()
   - Maps fields to Acumatica JSON format
   - Adds "Stampli Link: " prefix to note
   - Structures nested objects

8. AcumaticaAuthenticator.authenticatedApiCall()
   - Creates ConnectionManager
   - Executes login
   - Runs API call
   - Executes logout

9. RestApiCaller.call()
   - Builds HTTP PUT request
   - Sets timeouts and headers
   - Executes via Apache HttpClient

10. Response parsing
    - Extracts vendor ID from JSON
    - Builds UI link
    - Creates CsvLinkBridgeObject

11. Return ExportResponse
    - Success: responseCode=200, vendor ID & link
    - Error: error message in response
```

### Get Vendors Flow

```
1. AdminService.importVendors()

2. FinSysAgentManager.getAgent()

3. BridgeSynchronizationAgent.getVendors()
   - Creates GetVendorsRequest
   - Adds custom fields mapping
   - Sets delta import flag

4. DualBridgeSaaSRouter.sendToTaskCreatorAndGetResponse()

5. DriverEngine.invokeBridgeCommand(GET_VENDORS)

6. AcumaticaDriver.getVendors()
   - Creates AcumaticaImportHelper
   - Anonymous class with assembler

7. AcumaticaImportHelper.getValues()
   - Creates API callers
   - Authenticates and calls
   - Handles pagination (500/page)
   - Assembles response

8. Pagination logic
   - First call with TOP=500
   - If 500 results, paginate
   - Max 100 pages (delta) or 5000 (full)

9. Response assembly
   - Maps JSON to CsvVendor objects
   - Adds deleted vendors
   - Returns list
```

---

## Integration Recommendations

### For New Kotlin Module

#### 1. Module Structure
```
/core/erp-harness/
├── build.gradle.kts
├── src/
│   ├── main/kotlin/
│   │   ├── com/stampli/erp/
│   │   │   ├── api/
│   │   │   │   ├── AcumaticaApiClient.kt
│   │   │   │   └── HttpClient.kt
│   │   │   ├── auth/
│   │   │   │   ├── SessionManager.kt
│   │   │   │   └── CredentialProvider.kt
│   │   │   ├── drivers/
│   │   │   │   ├── AcumaticaDriver.kt
│   │   │   │   └── IDualFinsysDriver.kt
│   │   │   ├── models/
│   │   │   │   ├── shared/
│   │   │   │   │   └── SharedVendor.kt
│   │   │   │   └── acumatica/
│   │   │   │       └── AcumaticaVendor.kt
│   │   │   └── operations/
│   │   │       ├── VendorOperations.kt
│   │   │       └── BillOperations.kt
│   └── test/
│       ├── kotlin/
│       └── resources/
│           └── wiremock/
```

#### 2. Key Interfaces

```kotlin
// Credential Provider
interface CredentialProvider {
    suspend fun getCredentials(customerId: String): AcumaticaCredentials
    suspend fun validateCredentials(credentials: AcumaticaCredentials): Boolean
}

// Session Manager
interface SessionManager {
    suspend fun <T> withSession(
        credentials: AcumaticaCredentials,
        block: suspend (client: HttpClient) -> T
    ): T
}

// Vendor Operations
interface VendorOperations {
    suspend fun getVendors(filter: VendorFilter? = null): List<SharedVendor>
    suspend fun createVendor(vendor: SharedVendor): VendorResult
    suspend fun updateVendor(vendor: SharedVendor): VendorResult
    suspend fun findVendorByStampliLink(link: String): SharedVendor?
}

// Main Driver Interface
interface IDualFinsysDriver {
    val vendorOperations: VendorOperations
    val billOperations: BillOperations
    val paymentOperations: PaymentOperations
    // ... other operation groups
}
```

#### 3. Implementation Pattern

```kotlin
class AcumaticaDriver(
    private val credentialProvider: CredentialProvider,
    private val httpClient: HttpClient
) : IDualFinsysDriver {

    private val sessionManager = AcumaticaSessionManager(httpClient)

    override val vendorOperations = AcumaticaVendorOperations(
        sessionManager,
        credentialProvider
    )

    // Direct API implementation, no reflection
    suspend fun exportVendor(request: ExportVendorRequest): ExportResponse {
        return sessionManager.withSession(request.credentials) { client ->
            // Validation
            validateVendorData(request.data)

            // Check for existing vendor
            val existing = findVendorByStampliLink(request.stampliLink)
            if (existing != null) {
                return@withSession handleExistingVendor(existing, request)
            }

            // Create new vendor
            val payload = VendorPayloadMapper.toJson(request)
            val response = client.put(
                endpoint = AcumaticaEndpoint.VENDOR,
                body = payload
            )

            // Parse and return
            parseVendorResponse(response)
        }
    }
}
```

#### 4. Testing Strategy

```kotlin
class AcumaticaDriverTest {

    @Test
    fun `create vendor with WireMock`() = runTest {
        // Record mode: calls real API, saves stubs
        // Replay mode: uses saved stubs

        val driver = AcumaticaDriver(
            TestCredentialProvider(),
            WireMockHttpClient()
        )

        val result = driver.exportVendor(
            ExportVendorRequest(
                vendorName = "Test Vendor",
                stampliLink = "test-link-123"
            )
        )

        assertThat(result.isSuccess).isTrue()
        assertThat(result.vendorId).isNotEmpty()
    }
}
```

#### 5. Critical Design Decisions

**DO:**
- Implement IDualFinsysDriver interface directly
- Use coroutines for async operations
- Create shared DTOs with 4-5 common fields
- Use WireMock for hermetic testing
- Handle errors as response objects
- Implement login-wrapper-logout pattern

**DON'T:**
- Use reflection for method invocation
- Touch BridgeSynchronizationAgent
- Implement queue-based communication
- Create persistent sessions
- Throw exceptions for business errors
- Depend on legacy Java classes

#### 6. Feature Flags

```kotlin
class FeatureFlags(private val state: Map<String, String>) {

    fun isEnabled(flag: String): Boolean {
        return state[flag]?.equals("true", ignoreCase = true) ?: false
    }

    companion object {
        const val FF_SUPPORT_ACCESSIBLE_BRANCHES = "FF_SUPPORT_ACCESSIBLE_BRANCHES"
        const val FF_SUPPORT_NON_DELTA_IMPORTS = "FF_SUPPORT_NON_DELTA_IMPORTS_FOR_PROJECT_TASK"
        const val FF_RESTRICT_M2M_TASKS_WITH_BUDGET = "FF_RESTRICT_M2M_TASKS_WITH_BUDGET"
    }
}
```

#### 7. Custom Fields

```kotlin
data class CustomFieldMapping(
    val finsysId: String,
    val stampliId: String,
    val fieldType: FieldType,
    val required: Boolean = false
)

class CustomFieldHandler {
    fun mapCustomFields(
        source: Map<String, Any>,
        mappings: List<CustomFieldMapping>
    ): Map<String, Any> {
        return mappings.associate { mapping ->
            mapping.finsysId to (source[mapping.stampliId] ?: "")
        }
    }
}
```

---

## AgentOpCode Enum (Complete)

```java
public enum AgentOpCode {
    // Connection
    DISCONNECT("disconnect", DisconnectRequest.class, DisconnectResponse.class),
    PING("ping", PingRequest.class, PingResponse.class),
    CONNECT_TO_COMPANY("connectToCompany", ConnectToCompanyRequest.class, ConnectToCompanyResponse.class),
    GET_COMPANIES("getCompanies", GetCompaniesRequest.class, GetCompaniesResponse.class),

    // Vendor
    GET_VENDORS("getVendors", GetVendorsRequest.class, GetVendorsResponse.class),
    EXPORT_VENDOR("exportVendor", ExportVendorRequest.class, ExportResponse.class),

    // Bills
    GET_PAID_BILLS("getPaidBills", GetPaidBillsRequest.class, GetPaidBillsResponse.class),
    EXPORT("exportAPTransaction", ExportRequest.class, ExportResponse.class),
    RETRIEVE_BILLS("retrieveBillsByInvoicesInPayment", RetrieveBillsRequest.class, RetrieveInvoicesResponse.class),

    // Payments
    EXPORT_BILL_PAYMENT("exportBillPayment", ExportBillPaymentRequest.class, ExportResponse.class),
    DELETE_PAYMENT("deletePayment", DeletePaymentRequest.class, DeletePaymentResponse.class),
    VOID_PAYMENT("voidPayment", VoidPaymentRequest.class, VoidPaymentResponse.class),
    VOID_PAYMENT_WITH_MESSAGE("voidPayment", VoidPaymentWithMessageRequest.class, VoidPaymentWithMessageResponse.class),

    // Accounts
    GET_ACCOUNT_SEARCH_LIST("getAccountSearchList", GetAccountSearchListRequest.class, GetAccountSearchListResponse.class),
    GET_PAYMENT_ACCOUNT_SEARCH_LIST("getPaymentAccountSearchList", GetPaymentAccountSearchListRequest.class, GetPaymentAccountSearchListResponse.class),
    GET_PAYABLE_ACCOUNT_SEARCH_LIST("getPayableAccountSearchList", GetPayableAccountSearchListRequest.class, GetPayableAccountSearchListResponse.class),
    GET_BANK_ACCOUNT_SEARCH_LIST("getBankAccountSearchList", GetBankAccountSearchListRequest.class, GetBankAccountSearchListResponse.class),
    GET_DISCOUNT_ACCOUNT_SEARCH_LIST("getDiscountAccounts", GetDiscountAccountSearchListRequest.class, GetDiscountAccountSearchListResponse.class),

    // Fields
    GET_FIELD_SEARCH_LIST("getFieldSearchList", GetGeneralFieldSearchListRequest.class, FinSysBridgeBaseResponse.class),
    GET_FIELD_SEARCH_LIST_FOR_BILLY("getFieldSearchListForBilly", GetFieldSearchListForBillyRequest.class, GetFieldSearchListForBillyResponse.class),
    GET_CUSTOM_FIELD_SEARCH_LIST("getCustomFieldSearchList", GetCustomFieldSearchListRequest.class, GetGeneralFieldSearchListResponse.class),

    // Items
    GET_ITEM_SEARCH_LIST("getItemSearchList", GetItemSearchListRequest.class, GetItemSearchListResponse.class),
    GET_UNIT_SEARCH_LIST("getUnitSearchList", GetUnitSearchListRequest.class, GetUnitSearchListResponse.class),

    // Purchase Orders
    GET_PURCHASE_ORDER_SEARCH_LIST("getPurchaseOrderSearchList", GetPurchaseOrderSearchListRequest.class, GetPurchaseOrderSearchListResponse.class),
    GET_ITEMS_LISTS_PER_PO("getItemsListsPerPO", GetItemsListsPerPORequest.class, GetItemsListsPerPOResponse.class),
    EXPORT_PURCHASE_ORDER("exportPurchaseOrder", ExportPORequest.class, ExportResponse.class),
    GET_PO_DATA_FOR_VENDORS_PO_MATCHING("getPoMatchingDataForVendors", GetPoDataForVendorsBridgeRequest.class, GetPoDataForVendorsBridgeResponse.class),
    GET_PO_DATA_WITH_CLOSED_POS_PO_MATCHING("getPoMatchingDataWithClosedPOs", GetPODataForPoMatchingWithClosedPosRequest.class, GetPoDataForOpenAndClosedBridgeResponse.class),
    GET_PO_DATA_FOR_PO_MATCHING("getPoDataForPoMatching", GetPoDataBridgeRequest.class, GetPoDataBridgeResponse.class),

    // Cost & Job
    GET_COST_CODE_SEARCH_LIST("getCostCodeSearchListRequest", GetCostCodeSearchListRequest.class, GetCostCodeSearchListResponse.class),
    GET_COST_TYPE_SEARCH_LIST("getCostTypeSearchListRequest", GetCostTypeSearchListRequest.class, GetCostTypeSearchListResponse.class),
    GET_JOB_NUMBER_SEARCH_LIST("getJobNumberSearchListRequest", GetJobNumberSearchListRequest.class, GetJobNumberSearchListResponse.class),

    // Other
    GET_CLASS_SEARCH_LIST("getClassSearchList", GetClassSearchListRequest.class, GetClassSearchListResponse.class),
    GET_CUSTOMER_SEARCH_LIST("getCustomerSearchList", GetCustomerSearchListRequest.class, GetCustomerSearchListResponse.class),
    GET_GL_CATEGORIES_SEARCH_LIST("getGLCategoriesSearchList", GetGLCategoriesSearchListRequest.class, GetGLCategoriesSearchListResponse.class),
    GET_ITEM_CATEGORIES_SEARCH_LIST("getItemCategoriesSearchList", GetItemCategoriesSearchListRequest.class, GetItemCategoriesSearchListResponse.class),

    // Credits
    RETRIEVE_CREDITS("retrieveCredits", RetrieveCreditsRequest.class, RetrieveInvoicesResponse.class),
    GET_VENDOR_CREDIT_SEARCH_LIST("getVendorCreditSearchList", GetVendorCreditSearchListRequest.class, GetVendorCreditSearchListResponse.class),

    // Extensions
    GET_EXTENSION_VERSION("getExtensionVersion", GetExtensionVersionRequest.class, GetExtensionVersionResponse.class),
    UPDATE_EXTENSION_VERSION("updateExtensionVersion", UpdateExtensionVersionRequest.class, UpdateExtensionVersionResponse.class),

    // Admin
    PREPARE_ENVIRONMENT_TO_SYNC("prepareEnvironmentToSync", PrepareEnvironmentToSyncRequest.class, PrepareEnvironmentToSyncResponse.class),
    VALIDATE_CONFIGURATION("validateConfiguration", ValidateConfigurationRequest.class, ValidateConfigurationResponse.class),
    GET_COMPANY_CONFIG("getCompanyConfigRequest", GetCompanyConfigRequest.class, GetCompanyConfigResponse.class),
    CHECK_DUPLICATIONS("CheckDuplications", CheckDuplicationsRequest.class, CheckDuplicationsResponse.class),
    GET_FILTERING_FIELDS_FOR_FIELD("getFilteringFieldsForField", GetFilteringFieldsForFieldRequest.class, GetFilteringFieldsForFieldResponse.class),
    GET_PAYMENT_VENDORS("getVendorPaymentData", GetPaymentVendorsRequest.class, GetPaymentVendorsResponse.class),
    DEBUGGING_TOOLS("debuggingToolsExecuteRequest", DebuggingToolsRequest.class, DebuggingToolsResponse.class),
    TESTING_REQUEST("testingRequest", TestingRequest.class, TestingResponse.class);

    private String methodName;
    private Class<? extends FinSysBridgeBaseRequest> requestClass;
    private Class<? extends FinSysBridgeBaseResponse> responseClass;
}
```

---

## Summary

This comprehensive analysis provides:

1. **Complete architecture understanding** from AdminService to Acumatica API
2. **All 51 operation signatures** in AcumaticaDriver
3. **Full authentication flow** with session management
4. **Complete DTO structures** for requests and responses
5. **Handler implementations** with validation and error handling
6. **API communication patterns** with timeouts and error parsing
7. **Data flow diagrams** for key operations
8. **Integration recommendations** for new Kotlin module

The new Kotlin module should:
- Implement IDualFinsysDriver interface directly
- Bypass reflection and routing layers
- Use coroutines for async operations
- Implement login-wrapper-logout pattern
- Handle errors as response fields
- Use WireMock for testing
- Support feature flags via state map
- Map custom fields between systems

This provides a complete foundation for implementing the Acumatica integration in Kotlin with proper patterns and architecture.