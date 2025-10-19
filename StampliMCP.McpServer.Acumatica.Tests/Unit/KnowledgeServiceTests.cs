using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tests.Unit;

public class KnowledgeServiceTests
{
    [Fact]
    public async Task Operations_ParseOperationName_AsMethod()
    {
        ILogger<KnowledgeService> logger = NullLogger<KnowledgeService>.Instance;
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new KnowledgeService(logger, cache);

        var ops = await svc.GetOperationsByCategoryAsync("payments");
        ops.Should().NotBeEmpty();
        ops.Any(o => o.Method.Equals("voidBillPayment", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }
}

