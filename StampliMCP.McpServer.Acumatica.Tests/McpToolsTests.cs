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
        builder.Services.AddSingleton<KnowledgeService>();
        builder.Services.AddSingleton<SearchService>();
        _host = builder.Build();
        await _host.StartAsync();
        _knowledge = _host.Services.GetRequiredService<KnowledgeService>();
        _search = _host.Services.GetRequiredService<SearchService>();
    }

    [Fact]
    public async Task GetOperation_ExportVendor_ShouldReturnFullDetails()
    {
        // Act
        var result = await OperationTools.GetOperation(_knowledge, "exportVendor", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "operation");
        dict.Should().Contain(p => p.Name == "scanThese");
        dict.Should().Contain(p => p.Name == "goldenTest");
    }

    [Fact]
    public async Task ListOperations_NoCategory_ShouldReturnAll49Operations()
    {
        // Act
        var result = await OperationTools.ListOperations(_knowledge, null, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "total");
    }

    [Fact]
    public async Task ListOperations_VendorsCategory_ShouldReturn4Operations()
    {
        // Act
        var result = await OperationTools.ListOperations(_knowledge, "vendors", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var dict = result.GetType().GetProperties();
        dict.Should().Contain(p => p.Name == "category");
        dict.Should().Contain(p => p.Name == "operations");
    }

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

