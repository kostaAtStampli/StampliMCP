using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests;

/// <summary>
/// Tests that verify rich metadata exists for key operations
/// </summary>
public sealed class RichMetadataTests
{
    private readonly KnowledgeService _sut = new(NullLogger<KnowledgeService>.Instance);

    [Fact]
    public async Task ExportVendor_ShouldHave_FullFlowTrace()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.FlowTrace.Should().NotBeNullOrEmpty("exportVendor should have complete flow trace");
        operation.FlowTrace.Should().HaveCountGreaterThan(4, "should have service→router→driver→risk→handler layers");
        operation.FlowTrace.Should().Contain(f => f.Layer == "Service Entry");
        operation.FlowTrace.Should().Contain(f => f.Layer == "Router");
        operation.FlowTrace.Should().Contain(f => f.Layer == "Risk Control");
        operation.FlowTrace.Should().Contain(f => f.Layer == "Handler");
    }

    [Fact]
    public async Task ExportVendor_ShouldHave_OptionalFields()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.OptionalFields.Should().NotBeNullOrEmpty("exportVendor has optional fields");
        operation.OptionalFields.Should().ContainKeys("vendorId", "vendorClass", "terms", "currencyId");
    }

    [Fact]
    public async Task ExportVendor_ShouldHave_DtoLocations()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.RequestDtoLocation.Should().NotBeNull("should have request DTO pointer");
        operation.RequestDtoLocation!.File.Should().Contain("ExportVendorRequest.java");
        operation.ResponseDtoLocation.Should().NotBeNull("should have response DTO pointer");
        operation.ResponseDtoLocation!.File.Should().Contain("ExportResponse.java");
    }

    [Fact]
    public async Task ExportVendor_ShouldHave_StructuredHelpers()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.Helpers.Should().NotBeNullOrEmpty("exportVendor uses helper classes");
        operation.Helpers.Should().Contain(h => h.Class == "CreateVendorHandler");
        operation.Helpers.Should().Contain(h => h.Class == "VendorPayloadMapper");
        operation.Helpers.Should().Contain(h => h.Class == "AcumaticaAuthenticator");
        
        var handler = operation.Helpers.First(h => h.Class == "CreateVendorHandler");
        handler.Location.Should().NotBeNull();
        handler.Location.File.Should().Contain("CreateVendorHandler.java");
        handler.Purpose.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExportVendor_ShouldHave_ApiEndpointMetadata()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.ApiEndpoint.Should().NotBeNull("exportVendor should have API endpoint metadata");
        operation.ApiEndpoint!.Entity.Should().Be("Vendor");
        operation.ApiEndpoint.Method.Should().Be("PUT");
        operation.ApiEndpoint.UrlPattern.Should().Contain("entity");
        operation.ApiEndpoint.RequestBodyExample.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExportVendor_ShouldHave_ErrorCatalogReference()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.ErrorCatalogRef.Should().NotBeNullOrWhiteSpace("exportVendor should reference error catalog");
    }

    [Fact]
    public async Task ExportVendor_GoldenTest_ShouldHave_KeyTests()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.GoldenTest.Should().NotBeNull();
        operation.GoldenTest!.KeyTests.Should().NotBeNullOrEmpty("should have specific test methods to reference");
        operation.GoldenTest.KeyTests.Should().Contain(t => t.Method == "test_createVendorSuccessfully");
        operation.GoldenTest.KeyTests.Should().Contain(t => t.Method == "test_idempotencyReturnsExistingVendor");
        operation.GoldenTest.KeyTests.Should().Contain(t => t.Method == "test_exportWithDifferentLinkFails");
    }

    [Fact]
    public async Task GetVendors_ShouldHave_FullMetadata()
    {
        // Act
        var operation = await _sut.FindOperationAsync("getVendors");

        // Assert
        operation.Should().NotBeNull();
        operation!.FlowTrace.Should().NotBeNullOrEmpty("should have flow trace");
        operation.Helpers.Should().NotBeNullOrEmpty("should have helper classes");
        operation.RequestDtoLocation.Should().NotBeNull("should have request DTO location");
        operation.ResponseDtoLocation.Should().NotBeNull("should have response DTO location");
        operation.ApiEndpoint.Should().NotBeNull("should have API endpoint metadata");
        operation.ApiEndpoint!.Pagination.Should().BeTrue("getVendors supports pagination");
        operation.ApiEndpoint.DeltaSupport.Should().BeTrue("getVendors supports delta import");
    }

    [Fact]
    public async Task GetItemSearchList_ShouldHave_DualEndpointPattern()
    {
        // Act
        var operation = await _sut.FindOperationAsync("getItemSearchList");

        // Assert
        operation.Should().NotBeNull();
        operation!.Helpers.Should().NotBeNullOrEmpty();
        operation.Helpers.Should().Contain(h => h.Class == "StockItemResponseAssembler");
        operation.Helpers.Should().Contain(h => h.Class == "NonStockItemResponseAssembler");
        operation.ApiEndpoint.Should().NotBeNull();
        operation.ApiEndpoint!.Entities.Should().Contain(new[] { "StockItem", "NonStockItem" });
    }

    [Fact]
    public async Task ConnectToCompany_ShouldHave_RichMetadata()
    {
        // Act
        var operation = await _sut.FindOperationAsync("connectToCompany");

        // Assert
        operation.Should().NotBeNull();
        operation!.FlowTrace.Should().NotBeNullOrEmpty();
        operation.Helpers.Should().NotBeNullOrEmpty();
        operation.Helpers.Should().Contain(h => h.Class == "AcumaticaAuthenticator");
        operation.RequestDtoLocation.Should().NotBeNull();
        operation.ResponseDtoLocation.Should().NotBeNull();
    }

    [Theory]
    [InlineData("getVendorCreditSearchList")]
    [InlineData("getPaymentAccountSearchList")]
    public async Task SimpleOperations_ShouldHave_PatternReference(string methodName)
    {
        // Act
        var operation = await _sut.FindOperationAsync(methodName);

        // Assert
        operation.Should().NotBeNull();
        operation!.Pattern.Should().NotBeNullOrWhiteSpace("simple operations should reference a pattern");
        operation.ScanThese.Should().NotBeEmpty("should still have code pointer");
    }

    [Fact]
    public async Task AllOperations_ShouldHave_AtLeastOneScanPointer()
    {
        // Act
        var categories = await _sut.GetCategoriesAsync();
        var allOps = new List<Models.Operation>();
        
        foreach (var category in categories)
        {
            var ops = await _sut.GetOperationsByCategoryAsync(category.Name);
            allOps.AddRange(ops);
        }

        // Assert
        allOps.Should().HaveCountGreaterThanOrEqualTo(49, "should have all operations across all categories");
        allOps.Should().OnlyContain(op => op.ScanThese.Any(), "every operation must have at least one code pointer");
    }
}

