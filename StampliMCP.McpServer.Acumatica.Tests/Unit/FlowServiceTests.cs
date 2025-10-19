using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class FlowServiceTests
{
    [Fact]
    public async Task GetFlowAsync_ResolvesCaseInsensitiveNames()
    {
        ILogger<FlowService> logger = NullLogger<FlowService>.Instance;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new FlowService(logger, cache);

        var doc = await svc.GetFlowAsync("VENDOR_EXPORT_FLOW");
        doc.Should().NotBeNull();
        doc!.RootElement.TryGetProperty("description", out var _).Should().BeTrue();
    }
}

