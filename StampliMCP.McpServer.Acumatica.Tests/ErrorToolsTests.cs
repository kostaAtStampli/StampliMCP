using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica.Tools;
using Xunit.Internal;

namespace StampliMCP.McpServer.Acumatica.Tests;

/// <summary>
/// Tests for error catalog tool
/// </summary>
public sealed class ErrorToolsTests
{
    private readonly KnowledgeService _knowledge = new(
        NullLogger<KnowledgeService>.Instance,
        new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }));

    [Fact]
    public async Task GetErrors_NoOperation_ShouldReturnGeneralErrors()
    {
        // Act
        var result = await ErrorTools.GetErrors(null, _knowledge, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var props = result.GetType().GetProperties();
        props.Should().Contain(p => p.Name == "authenticationErrors");
        props.Should().Contain(p => p.Name == "apiErrors");
    }

    [Fact]
    public async Task GetErrors_ExportVendor_ShouldReturnValidationAndBusinessErrors()
    {
        // Act
        var result = await ErrorTools.GetErrors("exportVendor", _knowledge, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var props = result.GetType().GetProperties();
        props.Should().Contain(p => p.Name == "validation");
        props.Should().Contain(p => p.Name == "businessLogic");
        props.Should().Contain(p => p.Name == "authenticationErrors");
        props.Should().Contain(p => p.Name == "apiErrors");
    }

    [Fact]
    public async Task GetErrors_UnknownOperation_ShouldReturnGeneralErrorsWithMessage()
    {
        // Act
        var result = await ErrorTools.GetErrors("unknownOperation", _knowledge, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        var props = result.GetType().GetProperties();
        props.Should().Contain(p => p.Name == "message");
        props.Should().Contain(p => p.Name == "authenticationErrors");
    }
}

