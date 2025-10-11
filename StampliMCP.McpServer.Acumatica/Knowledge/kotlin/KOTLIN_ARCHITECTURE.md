# Kotlin ERP Harness Architecture

## Core Architecture Decision

### Approach: Implement IDualFinsysDriver Interface

**Why This Approach:**
- **Zero legacy code changes required** - DriverEngine can instantiate KotlinAcumaticaDriver via reflection
- **Drop-in replacement** - Kotlin becomes a seamless replacement for AcumaticaDriver
- **Incremental migration** - Can delegate unchanged operations to legacy driver
- **Proven integration point** - Uses existing reflection mechanism in DriverEngine

## Integration Flow

### How Kotlin Intercepts the Legacy Flow

1. **AdminService** calls FinSysAgentManager (unchanged)
2. **FinSysAgentManager** instantiates BridgeSynchronizationAgent (unchanged)
3. **BridgeSynchronizationAgent** builds request with `dualDriverName = "com.stampli.kotlin.driver.KotlinAcumaticaDriver"`
4. **DualBridgeSaaSRouter** routes to DriverEngine in SaaS mode (unchanged)
5. **DriverEngine** uses reflection to instantiate KotlinAcumaticaDriver ← **KOTLIN INTERCEPT POINT**
6. **KotlinAcumaticaDriver** implements the operation

### Key Integration Code

```kotlin
// Kotlin Implementation
package com.stampli.kotlin.driver

class KotlinAcumaticaDriver : IDualFinsysDriver {

    // Must have no-arg constructor for reflection
    constructor()

    // Implement all 51 methods from IDualFinsysDriver
    override fun exportVendor(request: ExportVendorRequest): ExportResponse {
        // Kotlin implementation
    }

    override fun getVendors(request: GetVendorsRequest): GetVendorsResponse {
        // Kotlin implementation
    }

    // Delegate unchanged operations
    private val legacyDriver = AcumaticaDriver()

    override fun connectToCompany(request: ConnectToCompanyRequest): ConnectToCompanyResponse {
        return legacyDriver.connectToCompany(request)
    }
}
```

## Authentication Strategy

### Reuse Existing Authentication Components

- **Class**: `com.stampli.driver.auth.AcumaticaAuthenticator`
- **Method**: `authenticatedApiCall`
- **Pattern**: Login-wrapper-logout per request
- **No session pooling**: Each operation gets fresh session

```kotlin
// Usage in Kotlin
val result = AcumaticaAuthenticator.authenticatedApiCall(
    request,
    apiCallerFactory
) { client ->
    // Your API call here
    apiCaller.call()
}
```

## Error Handling Philosophy

### Errors as Data, Not Exceptions

```kotlin
// CORRECT - Error as data
fun exportVendor(request: ExportVendorRequest): ExportResponse {
    val response = ExportResponse()

    if (vendorName.isBlank()) {
        response.error = "vendorName is required"
        return response
    }

    // Success case
    response.responseCode = 200
    response.response = vendorData
    return response
}

// WRONG - Don't throw exceptions
fun exportVendor(request: ExportVendorRequest): ExportResponse {
    if (vendorName.isBlank()) {
        throw ValidationException("vendorName is required") // DON'T DO THIS
    }
}
```

## Module Structure

### Location: `/mnt/c/STAMPLI4/core/kotlin-erp-harness/`

```
kotlin-erp-harness/
├── pom.xml                                    # Maven configuration
├── src/main/kotlin/com/stampli/kotlin/
│   ├── driver/
│   │   └── KotlinAcumaticaDriver.kt          # Main driver implementation
│   ├── adapters/
│   │   └── acumatica/
│   │       └── AcumaticaAdapter.kt           # Wraps legacy components
│   ├── mappers/
│   │   └── acumatica/
│   │       └── VendorMapper.kt               # Domain mapping
│   ├── domain/
│   │   ├── shared/
│   │   │   └── SharedVendor.kt               # Cross-ERP DTOs
│   │   └── acumatica/
│   │       └── AcumaticaVendor.kt            # ERP-specific DTOs
│   └── features/
│       └── shared/
│           └── BulkVendorImport.kt           # New features
```

## Implementation Phases

### Phase 1: Core Operations (Kotlin)
- `exportVendor` - Create/update vendors
- `getVendors` - Import vendors with pagination
- `exportAPTransaction` - Export bills
- `getPaidBills` - Import paid bills
- `connectToCompany` - Connection validation

### Phase 2: Delegate to Legacy
- Remaining 46 operations delegate to AcumaticaDriver
- Migrate incrementally as needed

## Critical Success Factors

1. **Exact method signatures** - Must match IDualFinsysDriver interface exactly
2. **No-arg constructor** - Required for reflection instantiation
3. **Error handling pattern** - Use response.error, not exceptions
4. **Authentication reuse** - Always use AcumaticaAuthenticator
5. **Registration** - Set dualDriverName correctly in requests

## Testing Strategy

- **Test Instance**: 63.32.187.185/StampliAcumaticaDB
- **Credentials**: admin/Password1
- **Approach**: Test against live instance initially
- **Future**: Add WireMock for hermetic testing

## Key Files to Reference

| Component | Legacy File | Purpose |
|-----------|------------|---------|
| Interface | IDualFinsysDriver.java | Interface to implement |
| Reflection | DriverEngine.java | Understand instantiation |
| Authentication | AcumaticaAuthenticator.java | Reuse for API calls |
| Handler | CreateVendorHandler.java:22-90 | Validation pattern |
| Import | AcumaticaImportHelper.java:64-120 | Pagination pattern |
| Mapper | VendorPayloadMapper.java:11-80 | JSON mapping |

## Success Metrics

- ✅ All 51 methods implemented or delegated
- ✅ DriverEngine can instantiate via reflection
- ✅ Authentication works with existing pattern
- ✅ Errors handled as response data
- ✅ Tests pass against test instance