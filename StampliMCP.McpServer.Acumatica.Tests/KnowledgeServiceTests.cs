using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests;

public sealed class KnowledgeServiceTests
{
    private readonly KnowledgeService _sut = new(NullLogger<KnowledgeService>.Instance);

    [Fact]
    public async Task GetCategories_ShouldReturn10Categories()
    {
        // Act
        var categories = await _sut.GetCategoriesAsync();

        // Assert
        categories.Should().HaveCount(10);
        categories.Select(c => c.Name).Should().Contain(new[] { "vendors", "items", "purchaseOrders", "other" });
    }

    [Fact]
    public async Task GetOperationsByCategory_Vendors_ShouldReturn4Operations()
    {
        // Act
        var operations = await _sut.GetOperationsByCategoryAsync("vendors");

        // Assert
        operations.Should().HaveCount(4);
        operations.Select(o => o.Method).Should().Contain(new[] { "exportVendor", "getVendors" });
    }

    [Fact]
    public async Task FindOperation_ExportVendor_ShouldReturnOperationWithCodePointers()
    {
        // Act
        var operation = await _sut.FindOperationAsync("exportVendor");

        // Assert
        operation.Should().NotBeNull();
        operation!.Method.Should().Be("exportVendor");
        operation.EnumName.Should().Be("EXPORT_VENDOR");
        operation.Category.Should().Be("vendors");
        operation.ScanThese.Should().NotBeEmpty();
        operation.ScanThese.Should().Contain(s => s.File.Contains("AcumaticaDriver"));
        operation.GoldenTest.Should().NotBeNull();
        operation.GoldenTest!.File.Should().Contain("AcumaticaDriverCreateVendorITest");
    }

    [Theory]
    [InlineData("getVendors")]
    [InlineData("getItemSearchList")]
    [InlineData("exportVendor")]
    [InlineData("connectToCompany")]
    public async Task FindOperation_ValidMethods_ShouldReturnOperations(string methodName)
    {
        // Act
        var operation = await _sut.FindOperationAsync(methodName);

        // Assert
        operation.Should().NotBeNull();
        operation!.Method.Should().BeEquivalentTo(methodName);
        operation.ScanThese.Should().NotBeEmpty("operation should have code pointers for LLM scanning");
    }

    [Fact]
    public async Task GetEnums_ShouldReturn6EnumTypes()
    {
        // Act
        var enums = await _sut.GetEnumsAsync();

        // Assert
        enums.Should().HaveCount(6);
        enums.Select(e => e.Name).Should().Contain(new[] { "VendorStatus", "AcumaticaItemType", "TransactionType" });
    }

    [Fact]
    public async Task GetTestConfig_ShouldReturnConfiguration()
    {
        // Act
        var config = await _sut.GetTestConfigAsync();

        // Assert
        config.Should().NotBeNull();
    }
}

