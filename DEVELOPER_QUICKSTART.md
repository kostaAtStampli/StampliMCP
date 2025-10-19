# 🚀 NEW DEVELOPER QUICKSTART

## Reality Check
✅ Tests in Kotlin (LiveErpTestBase + DSLs)  
✅ Implementation in Java (legacy driver)  
✅ Only exportVendor is Kotlin (everything else Java)  
✅ Use ENV1 for testing (63.32.187.185)  

---

## Your First Feature

### Step-by-Step TDD Workflow

1. **Ask MCP**: "How do I implement [feature]?"
2. **MCP guides**: Flow selection → Validation rules → File locations
3. **Write Kotlin test** in: `finsys-modern/kotlin-acumatica-driver/src/test/kotlin/`
4. **Run test**: `mvn test -Pmodern-live -Dtest=YourTest` (fails - RED ❌)
5. **Implement in Java**: `finsys-drivers/acumatica/src/main/java/`
6. **Run test again** (passes - GREEN ✅)
7. **Refactor** if needed

---

## Tools That Help

| Tool | Purpose |
|------|---------|
| `kotlin_tdd_workflow` | Complete TDD guidance with file locations and patterns |
| `modern_harness_guide` | DSL and harness documentation (LiveErpTestBase) |
| `validate_request` | Check your JSON payloads against Acumatica rules |
| `diagnose_error` | Fix problems with detailed error analysis |
| `get_kotlin_golden_reference` | View exportVendor Kotlin implementation (the only one) |

---

## Common Patterns

### Kotlin Test Pattern
```kotlin
package com.stampli.finsys.modern.acumatica

import com.stampli.finsys.modern.testing.LiveErpTestBase
import com.stampli.finsys.modern.testing.Dsl
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.Tag
import org.assertj.core.api.Assertions.assertThat

class YourFeatureTest : LiveErpTestBase() {
    
    @Test
    @Tag("live-acumatica")
    fun `test feature works`() {
        // Arrange - Build test data with DSL
        val data = Dsl.vendor("C1") {
            vendorId("TEST-${System.currentTimeMillis()}")
            name("Test Vendor")
            address {
                line1("123 Main St")
                city("San Francisco")
            }
        }
        
        // Act - Call Java implementation
        val request = createRequest(data.toMap())
        val response = driver.yourFeature(request)
        
        // Assert
        assertThat(response.error).isNull()
        assertThat(response.response).isNotNull()
        
        // Record for analysis
        recordExport(ExportKey.from(response))
    }
}
```

### Java Implementation Pattern
```java
package com.stampli.driver.handlers;

import com.stampli.driver.ApiCallerFactory;
import com.stampli.driver.AcumaticaDriver;
import com.stampli.request.ExportRequest;
import com.stampli.response.ExportResponse;

public class YourFeatureHandler {
    
    public static ExportResponse execute(
        ApiCallerFactory factory,
        ExportRequest request
    ) {
        ExportResponse response = new ExportResponse();
        
        try {
            // 1. Validate
            String error = validateRequest(request);
            if (error != null) {
                response.setError(error);
                return response;
            }
            
            // 2. Map to Acumatica JSON
            String payload = mapToJson(request);
            
            // 3. Call API with authentication wrapper
            ResponseData apiResponse = AcumaticaAuthenticator.authenticatedApiCall(
                request,
                factory,
                (client) -> {
                    ApiCaller caller = factory.createPutRestApiCaller(
                        request,
                        AcumaticaEndpoint.YOUR_ENDPOINT,
                        new AcumaticaUrlSuffixAssembler(),
                        payload
                    );
                    return caller.call(client);
                }
            );
            
            // 4. Handle response
            if (!apiResponse.isSuccessful()) {
                response.setError("API call failed: " + apiResponse.getContent());
                return response;
            }
            
            response.setResponseCode(200);
            // ... build response object
            
        } catch (Exception e) {
            response.setError(e.getMessage());
        }
        
        return response;
    }
    
    private static String validateRequest(ExportRequest request) {
        // Add validation logic
        if (request.getRawData() == null) {
            return "Missing data";
        }
        // ... more validation
        return null;
    }
    
    private static String mapToJson(ExportRequest request) {
        JSONObject root = new JSONObject();
        // Map raw data to Acumatica format
        // Use putValue() helper for nested {"value": ...} structure
        return root.toString();
    }
}
```

---

## Environment Details

### ENV1 (Test Environment)
- **Host**: `http://63.32.187.185/StampliAcumaticaDB`
- **User**: `admin`
- **Password**: `Password1`
- **Subsidiary**: `StampliCompany`
- **Note**: Test data may be deleted between runs - always use DSLs to create test data

### Maven Profiles
```bash
# Integration tests (no live ERP)
mvn test -Pmodern-it

# Live ERP tests against ENV1
mvn test -Pmodern-live

# Vendor probe only (always green)
mvn test -Pmodern-live-env1

# Run specific test
mvn test -Pmodern-live -Dtest=YourFeatureTest

# Run with recording
mvn test -Pmodern-live -Dlive.outDir=./test-results
```

---

## Module Structure

```
finsys-modern/
├── kotlin-acumatica-driver/         # Acumatica driver + tests
│   ├── src/main/kotlin/             # Main code (only exportVendor)
│   └── src/test/kotlin/             # 👈 WRITE YOUR TESTS HERE
│       └── com/stampli/finsys/modern/acumatica/
│
├── kotlin-drivers-common/           # Test harness
│   └── src/main/kotlin/.../testing/
│       ├── LiveErpTestBase.kt       # Base class for tests
│       └── Dsl.kt                   # DSL builders
│
finsys-drivers/
└── acumatica/
    └── src/main/java/               # 👈 IMPLEMENT IN JAVA HERE
        └── com/stampli/driver/
```

---

## DSL Examples

### Vendor DSL
```kotlin
val vendor = Dsl.vendor("C1") {
    vendorId("V-${timestamp}")
    name("Acme Corporation")
    vendorClass("VENDOR")
    customField("Department", "Finance")
    
    address {
        line1("123 Main St")
        city("San Francisco")
        state("CA")
        zip("94105")
    }
    
    bank {
        accountId("CHK-001")
        routingNumber("123456789")
        bankName("Chase Bank")
    }
}

// Convert to Map for Java driver
val rawData = vendor.toMap()
```

### Purchase Order DSL
```kotlin
val po = Dsl.purchaseOrder("C1") {
    poNumber("PO-${timestamp}")
    vendorId("V-123")
    date(LocalDate.now())
    
    line {
        itemId("WIDGET")
        quantity(BigDecimal("10"))
        unitPrice(BigDecimal("25.50"))
        description("Test Widget")
    }
}
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Connection failures** | Check ENV1 is accessible: `ping 63.32.187.185` |
| **Test data conflicts** | Use `System.currentTimeMillis()` in IDs for uniqueness |
| **Cleanup failures** | LiveErpTestBase logs errors but continues - check logs |
| **Recording not working** | Verify `-Dlive.outDir` path is writable |
| **DSL compilation errors** | Ensure `kotlin-drivers-common` is on classpath |
| **"Not found" errors** | Verify Kotlin files exist in `finsys-modern/` (not `kotlin-drivers/`) |

---

## Next Steps

1. **Ask MCP** for guidance: Call `kotlin_tdd_workflow` tool with your feature description
2. **Read golden reference**: Call `get_kotlin_golden_reference` to see exportVendor patterns
3. **Check harness**: Call `modern_harness_guide` for LiveErpTestBase and DSL docs
4. **Start coding**: Write test (RED) → Implement (GREEN) → Refactor

---

## Quick Reference Card

```
📁 Where to write tests:    finsys-modern/kotlin-acumatica-driver/src/test/kotlin/
📁 Where to implement:      finsys-drivers/acumatica/src/main/java/
🧪 Base class:              LiveErpTestBase
🏗️  DSL:                    Dsl.vendor, Dsl.purchaseOrder
🔧 Maven:                   mvn test -Pmodern-live -Dtest=YourTest
🌍 Environment:             ENV1 (63.32.187.185)
✅ Kotlin operations:       Only exportVendor
📦 Package (tests):         com.stampli.finsys.modern.acumatica
📦 Package (impl):          com.stampli.driver
```

**Welcome aboard! 🎉**

