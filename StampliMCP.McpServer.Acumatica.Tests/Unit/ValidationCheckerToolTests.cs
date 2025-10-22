using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Models;
using StampliMCP.McpServer.Acumatica.Module;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.McpServer.Unified.Tools;
using StampliMCP.Shared.Erp;
using System.Text.Json;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class ValidationCheckerToolTests
{
    [Fact]
    public async Task VendorId_Over15_ShouldFail()
    {
        var registry = CreateRegistry();
        var payload = "{\"VendorID\":\"1234567890123456789\", \"vendorName\": \"ABC\"}"; // 19 chars
        var call = await ErpValidationTool.Execute("acumatica", "exportVendor", payload, registry, default);
        var node = call.StructuredContent!; // expect structured
        var result = JsonSerializer.Deserialize<ValidationResult>(node["result"]);
        result!.IsValid.Should().BeFalse();
        result.Errors.Any(e => e.Field == "VendorID" && e.Rule == "max_length_15").Should().BeTrue();
    }

    [Fact]
    public async Task PageSize_Over2000_ShouldFail()
    {
        var registry = CreateRegistry();
        var payload = "{\"pageSize\": 3000}";
        var call = await ErpValidationTool.Execute("acumatica", "importVendors", payload, registry, default);
        var node = call.StructuredContent!;
        var result = JsonSerializer.Deserialize<ValidationResult>(node["result"]);
        result!.IsValid.Should().BeFalse();
        result.Errors.Any(e => e.Field == "pageSize" && e.Rule == "max_pagination_2000").Should().BeTrue();
    }

    private static ErpRegistry CreateRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();

        var module = new AcumaticaModule();
        services.AddSingleton<IErpModule>(module);
        module.RegisterServices(services);

        services.AddSingleton<ErpRegistry>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ErpRegistry>();
    }
}
