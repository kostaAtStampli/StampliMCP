using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Models;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.McpServer.Acumatica.Tools;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class ValidationCheckerToolTests
{
    [Fact]
    public async Task VendorId_Over15_ShouldFail()
    {
        ILogger<FlowService> fl = NullLogger<FlowService>.Instance;
        ILogger<KnowledgeService> kl = NullLogger<KnowledgeService>.Instance;
        ILogger<FuzzyMatchingService> fuzzyLogger = NullLogger<FuzzyMatchingService>.Instance;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new FuzzyMatchingConfig();
        var fuzzyMatcher = new FuzzyMatchingService(config, fuzzyLogger);
        var flow = new FlowService(fl, cache, fuzzyMatcher);
        var knowledge = new KnowledgeService(kl, cache);

        var payload = "{\"VendorID\":\"1234567890123456789\", \"vendorName\": \"ABC\"}"; // 19 chars
        var call = await ValidationCheckerTool.Execute("exportVendor", payload, flow, knowledge, fuzzyMatcher, default);
        var node = call.StructuredContent!; // expect structured
        var result = System.Text.Json.JsonSerializer.Deserialize<StampliMCP.McpServer.Acumatica.Models.ValidationResult>(node["result"]);
        result!.IsValid.Should().BeFalse();
        result.Errors.Any(e => e.Field == "VendorID" && e.Rule == "max_length_15").Should().BeTrue();
    }

    [Fact]
    public async Task PageSize_Over2000_ShouldFail()
    {
        ILogger<FlowService> fl = NullLogger<FlowService>.Instance;
        ILogger<KnowledgeService> kl = NullLogger<KnowledgeService>.Instance;
        ILogger<FuzzyMatchingService> fuzzyLogger = NullLogger<FuzzyMatchingService>.Instance;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new FuzzyMatchingConfig();
        var fuzzyMatcher = new FuzzyMatchingService(config, fuzzyLogger);
        var flow = new FlowService(fl, cache, fuzzyMatcher);
        var knowledge = new KnowledgeService(kl, cache);

        var payload = "{\"pageSize\": 3000}";
        var call = await ValidationCheckerTool.Execute("importVendors", payload, flow, knowledge, fuzzyMatcher, default);
        var node = call.StructuredContent!;
        var result = System.Text.Json.JsonSerializer.Deserialize<StampliMCP.McpServer.Acumatica.Models.ValidationResult>(node["result"]);
        result!.IsValid.Should().BeFalse();
        result.Errors.Any(e => e.Field == "pageSize" && e.Rule == "max_pagination_2000").Should().BeTrue();
    }
}
