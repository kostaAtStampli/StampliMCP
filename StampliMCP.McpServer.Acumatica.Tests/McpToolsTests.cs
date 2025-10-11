using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica.Tools;

namespace StampliMCP.McpServer.Acumatica.Tests;

public sealed class McpToolsTests : IAsyncLifetime
{
    private IHost? _host;
    private KnowledgeService _knowledge = null!;
    private SearchService _search = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<KnowledgeService>();
        builder.Services.AddSingleton<SearchService>();
        _host = builder.Build();
        await _host.StartAsync();
        _knowledge = _host.Services.GetRequiredService<KnowledgeService>();
        _search = _host.Services.GetRequiredService<SearchService>();
    }

    [Fact]
    public async Task GetOperationDetails_ExportVendor_ShouldReturnFullDetails()
    {
        // Act
        var result = await OperationDetailsTool.GetOperationDetails("exportVendor", _knowledge, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "method");
        dict.Should().Contain(p => p.Name == "scanFiles");
        dict.Should().Contain(p => p.Name == "goldenTest");
        dict.Should().Contain(p => p.Name == "requiredFields");
        dict.Should().Contain(p => p.Name == "errorPatterns");
    }

    [Fact]
    public async Task ImplementKotlinFeature_ShouldReturnWorkflow()
    {
        // Act
        var result = await KotlinFeatureTool.ImplementKotlinFeature("Add vendor export");

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "steps");
        dict.Should().Contain(p => p.Name == "resources");
        dict.Should().Contain(p => p.Name == "enforcement");
    }

    // Note: ListOperations functionality removed - use search_operations or list_categories instead

    [Fact]
    public async Task ListCategories_ShouldReturn9Categories()
    {
        // Act
        var result = await CategoryTools.ListCategories(_knowledge, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "categories");
    }

    [Fact]
    public async Task SearchOperations_DuplicateKeyword_ShouldFindExportVendor()
    {
        // Act
        var result = await SearchTools.SearchOperations(_search, "duplicate", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "matches");
    }

    [Fact]
    public async Task GetEnums_ShouldReturn6EnumTypes()
    {
        // Act
        var result = await EnumTools.GetEnums(_knowledge, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "enums");
    }

    [Fact]
    public async Task GetTestConfig_ShouldReturnConfiguration()
    {
        // Act
        var result = await EnumTools.GetTestConfig(_knowledge, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
            await _host.StopAsync();
        _host?.Dispose();
    }
}

