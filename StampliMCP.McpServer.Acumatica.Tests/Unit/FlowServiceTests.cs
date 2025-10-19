using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class FlowServiceTests
{
    [Fact]
    public async Task GetFlowAsync_ResolvesCaseInsensitiveNames()
    {
        ILogger<FlowService> logger = NullLogger<FlowService>.Instance;
        ILogger<FuzzyMatchingService> fuzzyLogger = NullLogger<FuzzyMatchingService>.Instance;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new FuzzyMatchingConfig();
        var fuzzyMatcher = new FuzzyMatchingService(config, fuzzyLogger);
        var svc = new FlowService(logger, cache, fuzzyMatcher);

        var doc = await svc.GetFlowAsync("VENDOR_EXPORT_FLOW");
        doc.Should().NotBeNull();
        doc!.RootElement.TryGetProperty("description", out var _).Should().BeTrue();
    }
}
