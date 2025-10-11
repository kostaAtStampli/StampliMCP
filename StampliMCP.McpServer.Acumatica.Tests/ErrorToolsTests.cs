using FluentAssertions;
using StampliMCP.McpServer.Acumatica.Tools;

namespace StampliMCP.McpServer.Acumatica.Tests;

/// <summary>
/// Tests for error catalog tool
/// </summary>
public sealed class ErrorToolsTests
{
    [Fact]
    public async Task GetErrors_NoOperation_ShouldReturnGeneralErrors()
    {
        // Act
        var result = await ErrorTools.GetErrors(null, CancellationToken.None);

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
        var result = await ErrorTools.GetErrors("exportVendor", CancellationToken.None);

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
        var result = await ErrorTools.GetErrors("unknownOperation", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var props = result.GetType().GetProperties();
        props.Should().Contain(p => p.Name == "message");
        props.Should().Contain(p => p.Name == "authenticationErrors");
    }
}

