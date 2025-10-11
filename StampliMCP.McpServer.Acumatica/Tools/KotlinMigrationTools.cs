using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class KotlinMigrationTools
{
    [McpServerTool(Name = "get_kotlin_flow", UseStructuredContent = true)]
    [Description("Get concise Kotlin migration flow for a specific category of operations")]
    public static async ValueTask<object> GetKotlinFlow(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("Category name (vendors, payments, purchaseOrders, items, accounts, fields, admin)")] string category,
        CancellationToken cancellationToken)
    {
        var operations = await knowledge.GetOperationsByCategoryAsync(category, cancellationToken);

        // Return super concise flow summary
        return category.ToLowerInvariant() switch
        {
            "vendors" => new
            {
                category,
                interceptPoint = "DriverEngine → KotlinAcumaticaDriver.exportVendor/getVendors",
                flow = new[]
                {
                    "1. BridgeSynchronizationAgent builds request",
                    "2. DriverEngine uses reflection to instantiate KotlinAcumaticaDriver",
                    "3. Kotlin validates using exact error messages",
                    "4. Reuse AcumaticaAuthenticator.authenticatedApiCall",
                    "5. Reuse VendorPayloadMapper for JSON"
                },
                reuseComponents = new[] { "AcumaticaAuthenticator", "VendorPayloadMapper", "ApiCallerFactory" },
                criticalPattern = "response.error = 'message' (no exceptions!)"
            },
            "payments" => new
            {
                category,
                interceptPoint = "DriverEngine → KotlinAcumaticaDriver.exportBillPayment/voidPayment",
                flow = new[]
                {
                    "1. Build payment request with bill references",
                    "2. Kotlin intercepts at IDualFinsysDriver method",
                    "3. Validate payment account and amounts",
                    "4. Auth wrap with AcumaticaAuthenticator",
                    "5. POST to Payment endpoint"
                },
                reuseComponents = new[] { "AcumaticaAuthenticator", "PaymentValidator", "BillPaymentMapper" },
                criticalPattern = "Handle partial payments and voids"
            },
            "purchaseorders" => new
            {
                category,
                interceptPoint = "DriverEngine → KotlinAcumaticaDriver.exportPurchaseOrder",
                flow = new[]
                {
                    "1. PO matching logic in Kotlin",
                    "2. Validate PO lines and amounts",
                    "3. Check duplicates via getMatchingPoByStampliLink",
                    "4. Export with line items",
                    "5. Return PO number"
                },
                reuseComponents = new[] { "AcumaticaAuthenticator", "PurchaseOrderMapper", "POMatchingHelper" },
                criticalPattern = "Idempotent - return existing PO if duplicate"
            },
            _ => new
            {
                category,
                interceptPoint = $"DriverEngine → KotlinAcumaticaDriver.{category}Methods",
                flow = new[]
                {
                    "1. Request built by BridgeSynchronizationAgent",
                    "2. Kotlin intercepts via reflection",
                    "3. Delegate to legacy AcumaticaDriver initially",
                    "4. Migrate incrementally as needed"
                },
                reuseComponents = new[] { "AcumaticaDriver (delegation)", "AcumaticaAuthenticator" },
                criticalPattern = "Start with delegation, migrate later"
            }
        };
    }

    [McpServerTool(Name = "get_implementation_guide", UseStructuredContent = true)]
    [Description("Get implementation guide for a specific Kotlin migration operation")]
    public static async ValueTask<object> GetImplementationGuide(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("Method name (e.g., exportVendor, getVendors)")] string methodName,
        CancellationToken cancellationToken)
    {
        var operation = await knowledge.FindOperationAsync(methodName, cancellationToken);

        if (operation is null)
            return new { error = $"Operation '{methodName}' not found" };

        // Build concise implementation guide
        return new
        {
            methodName,
            signature = GetMethodSignature(methodName),
            validationRules = GetValidationRules(methodName),
            errorMessages = GetErrorMessages(methodName),
            authPattern = "AcumaticaAuthenticator.authenticatedApiCall(request, factory) { apiCaller.call() }",
            kotlinExample = GetKotlinExample(methodName),
            testPattern = @"
@Test
fun `${methodName} validates required fields`() {
    val request = createRequest(invalidData)
    val response = driver.${methodName}(request)
    assertEquals(""exact error from MCP"", response.error)
}",
            reuseComponents = GetReuseComponents(operation)
        };
    }

    [McpServerTool(Name = "get_method_signatures", UseStructuredContent = true)]
    [Description("Get method signatures for a specific category (not all 51!)")]
    public static ValueTask<object> GetMethodSignatures(
        [Description("Category name (vendors, payments, etc.) or 'critical' for key methods only")] string category)
    {
        var signatures = category.ToLowerInvariant() switch
        {
            "vendors" => new[]
            {
                "GetVendorsResponse getVendors(GetVendorsRequest request)",
                "ExportResponse exportVendor(ExportVendorRequest request)",
                "CsvLinkBridgeObject getMatchingVendorByStampliLink(RetrieveVendorByLinkRequest request)",
                "CsvLinkBridgeObject getDuplicateVendorById(RetrieveVendorByIdRequest request)"
            },
            "payments" => new[]
            {
                "ExportResponse exportBillPayment(ExportBillPaymentRequest request)",
                "VoidPaymentResponse voidPayment(VoidPaymentRequest request)",
                "GetPaidBillsResponse getPaidBills(GetPaidBillsRequest request)",
                "RetrieveBillPaymentsResponse retrieveBillPayments(RetrieveBillPaymentsRequest request)",
                "GetVendorCreditSearchListResponse getVendorCreditSearchList(GetVendorCreditSearchListRequest request)"
            },
            "purchaseorders" => new[]
            {
                "ExportResponse exportPurchaseOrder(ExportPORequest request)",
                "GetPurchaseOrderSearchListResponse getPurchaseOrderSearchList(GetPurchaseOrderSearchListRequest request)",
                "CsvLinkBridgeObject getMatchingPoByStampliLink(RetrievePoByLinkRequest request)",
                "GetPoDataBridgeResponse getPoDataForPoMatching(GetPoDataBridgeRequest request)"
            },
            "critical" => new[]
            {
                "ConnectToCompanyResponse connectToCompany(ConnectToCompanyRequest request)",
                "ExportResponse exportVendor(ExportVendorRequest request)",
                "GetVendorsResponse getVendors(GetVendorsRequest request)",
                "ExportResponse exportAPTransaction(ExportRequest request)",
                "ExportResponse exportBillPayment(ExportBillPaymentRequest request)"
            },
            _ => new[] { $"Use specific category: vendors, payments, purchaseOrders, items, accounts, fields, admin, critical" }
        };

        return ValueTask.FromResult<object>(new
        {
            category,
            count = signatures.Length,
            signatures,
            kotlinNote = "All must return response object with error field, no exceptions!"
        });
    }

    [McpServerTool(Name = "get_error_patterns", UseStructuredContent = true)]
    [Description("Get error handling patterns for Kotlin implementation")]
    public static ValueTask<object> GetErrorPatterns(
        [Description("Error type: validation, api, auth, or business")] string errorType)
    {
        object patterns = errorType.ToLowerInvariant() switch
        {
            "validation" => new
            {
                type = "Validation Errors",
                principle = "Set response.error with exact message",
                examples = new[]
                {
                    new { field = "vendorName", error = "vendorName is required" },
                    new { field = "vendorName", error = "vendorName exceeds maximum length of 60 characters" },
                    new { field = "stampliUrl", error = "stampliurl is required" }
                },
                kotlinPattern = "if (vendorName.isNullOrBlank()) { response.error = \"vendorName is required\"; return response }"
            },
            "api" => new
            {
                type = "API Errors",
                principle = "Wrap HTTP status in error message",
                examples = new[]
                {
                    new { code = 400, error = "Acumatica returned 400: Bad Request" },
                    new { code = 401, error = "Acumatica returned 401: Unauthorized" },
                    new { code = 500, error = "Acumatica returned 500: Internal Server Error" }
                },
                kotlinPattern = "if (!result.isSuccessful) { response.error = \"Acumatica returned ${result.responseCode}\"; return response }"
            },
            "business" => new
            {
                type = "Business Logic Errors",
                principle = "Idempotent operations return success for duplicates",
                examples = new[]
                {
                    new { scenario = "duplicate_vendor_same_link", action = "Return existing vendor ID (success)" },
                    new { scenario = "duplicate_vendor_diff_link", action = "Return error with mismatch message" }
                },
                kotlinPattern = "Idempotent: return existing ID, not error"
            },
            _ => new { error = "Use errorType: validation, api, auth, or business" }
        };

        return ValueTask.FromResult(patterns);
    }

    // Helper methods
    private static string GetMethodSignature(string methodName) => methodName switch
    {
        "exportVendor" => "ExportResponse exportVendor(ExportVendorRequest request)",
        "getVendors" => "GetVendorsResponse getVendors(GetVendorsRequest request)",
        "exportBillPayment" => "ExportResponse exportBillPayment(ExportBillPaymentRequest request)",
        "connectToCompany" => "ConnectToCompanyResponse connectToCompany(ConnectToCompanyRequest request)",
        _ => $"{methodName}Response {methodName}({methodName}Request request)"
    };

    private static object GetValidationRules(string methodName) => methodName switch
    {
        "exportVendor" => new { vendorName = "required, max 60 chars", stampliUrl = "required" },
        "exportBillPayment" => new { paymentAccount = "required", amount = "required, > 0" },
        _ => new { }
    };

    private static object GetErrorMessages(string methodName) => methodName switch
    {
        "exportVendor" => new[] { "vendorName is required", "vendorName exceeds maximum length of 60 characters" },
        "exportBillPayment" => new[] { "Payment account is required", "Amount must be greater than 0" },
        _ => Array.Empty<string>()
    };

    private static string GetKotlinExample(string methodName) => methodName switch
    {
        "exportVendor" => @"
override fun exportVendor(request: ExportVendorRequest): ExportResponse {
    val response = ExportResponse()
    val vendorName = request.rawData?.get(""vendorName"")
    if (vendorName.isNullOrBlank()) {
        response.error = ""vendorName is required""
        return response
    }
    // Reuse authentication
    val result = AcumaticaAuthenticator.authenticatedApiCall(request, factory) {
        apiCaller.call()
    }
    if (!result.isSuccessful) {
        response.error = ""Acumatica returned ${result.responseCode}""
        return response
    }
    response.response = parseResult(result.content)
    return response
}",
        _ => "// Implement using pattern from exportVendor"
    };

    private static string[] GetReuseComponents(Models.Operation operation) => operation.Method switch
    {
        "exportVendor" => ["AcumaticaAuthenticator", "VendorPayloadMapper", "ApiCallerFactory"],
        "getVendors" => ["AcumaticaAuthenticator", "VendorResponseAssembler", "ImportHelper"],
        _ => ["AcumaticaAuthenticator", "ApiCallerFactory"]
    };

    [McpServerTool(Name = "get_all_method_signatures", UseStructuredContent = true)]
    [Description("Get complete list of all 51 IDualFinsysDriver methods with implementation strategy")]
    public static async ValueTask<object> GetAllMethodSignatures(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("Filter: 'all', 'phase1', 'phase2', or category name")] string filter = "all",
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Knowledge", "method-signatures.json"),
            cancellationToken);

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        var signatures = data?["IDualFinsysDriver"] as JsonElement?;

        if (signatures == null) return new { error = "Could not load method signatures" };

        var methodList = signatures.Value.GetProperty("completeMethodList").EnumerateArray()
            .Select(m => new
            {
                signature = m.GetProperty("signature").GetString(),
                category = m.GetProperty("category").GetString(),
                phase = m.GetProperty("phase").GetString(),
                implement = m.TryGetProperty("implement", out var impl) && impl.GetBoolean(),
                delegate_ = m.TryGetProperty("delegate", out var del) && del.GetBoolean()
            }).ToList();

        var filtered = filter.ToLowerInvariant() switch
        {
            "all" => methodList,
            "phase1" => methodList.Where(m => m.phase == "1").ToList(),
            "phase2" => methodList.Where(m => m.phase == "2").ToList(),
            _ => methodList.Where(m => m.category == filter.ToLowerInvariant()).ToList()
        };

        return new
        {
            filter,
            totalMethods = 51,
            filteredCount = filtered.Count,
            phase1Count = methodList.Count(m => m.phase == "1"),
            phase2Count = methodList.Count(m => m.phase == "2"),
            methods = filtered,
            implementationNote = "Phase 1: Implement in Kotlin. Phase 2: Delegate to legacy driver initially."
        };
    }

    [McpServerTool(Name = "get_reflection_details", UseStructuredContent = true)]
    [Description("Get details about how DriverEngine uses reflection to instantiate Kotlin driver")]
    public static async ValueTask<object> GetReflectionDetails(
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Knowledge", "reflection-mechanism.json"),
            cancellationToken);

        return JsonSerializer.Deserialize<object>(json) ?? new { error = "Could not load reflection details" };
    }

    [McpServerTool(Name = "get_integration_strategy", UseStructuredContent = true)]
    [Description("Get the complete integration strategy including phases and delegation approach")]
    public static async ValueTask<object> GetIntegrationStrategy(
        [Description("Section: 'all', 'phases', 'authentication', 'reuse', 'deployment'")] string section = "all",
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Knowledge", "integration-strategy.json"),
            cancellationToken);

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (data == null) return new { error = "Could not load integration strategy" };

        return section.ToLowerInvariant() switch
        {
            "phases" => data.ContainsKey("implementationPhases") ? data["implementationPhases"] : new { },
            "authentication" => data.ContainsKey("authenticationStrategy") ? data["authenticationStrategy"] : new { },
            "reuse" => data.ContainsKey("reuseStrategy") ? data["reuseStrategy"] : new { },
            "deployment" => data.ContainsKey("deploymentStrategy") ? data["deploymentStrategy"] : new { },
            _ => data
        };
    }

    [McpServerTool(Name = "get_kotlin_test_pattern", UseStructuredContent = true)]
    [Description("Get Kotlin-specific test patterns and setup")]
    public static async ValueTask<object> GetKotlinTestPattern(
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Knowledge", "test-config.json"),
            cancellationToken);

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        if (data == null) return new { error = "Could not load test config" };

        return new
        {
            testEnvironment = data.ContainsKey("testCustomer") ? data["testCustomer"] : null,
            kotlinPatterns = data.ContainsKey("kotlinTestPatterns") ? data["kotlinTestPatterns"] : null,
            connectionConfig = data.ContainsKey("connectionConfigUsage") ? data["connectionConfigUsage"] : null,
            testingStrategy = data.ContainsKey("testingStrategy") ? data["testingStrategy"] : null,
            note = "Use these patterns for Kotlin test implementation"
        };
    }

    [McpServerTool(Name = "get_complete_flow", UseStructuredContent = true)]
    [Description("Get complete flow trace from AdminService to Kotlin driver")]
    public static async ValueTask<object> GetCompleteFlow(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("Method name (e.g., 'exportVendor')")] string methodName,
        CancellationToken cancellationToken = default)
    {
        var reflectionJson = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Knowledge", "reflection-mechanism.json"),
            cancellationToken);

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(reflectionJson);

        if (data == null) return new { error = "Could not load flow data" };

        // Get the complete flow trace
        var flowTrace = data.ContainsKey("completeFlowTrace") ? data["completeFlowTrace"] : null;

        // Also get operation-specific flow if available
        var operation = await knowledge.FindOperationAsync(methodName, cancellationToken);

        return new
        {
            method = methodName,
            completeFlow = flowTrace,
            operationFlow = operation?.FlowTrace,
            kotlinInterceptPoint = "Step 5-6: DriverEngine.invokeBridgeCommand → KotlinAcumaticaDriver." + methodName,
            registration = "request.dualDriverName = 'com.stampli.kotlin.driver.KotlinAcumaticaDriver'"
        };
    }
}