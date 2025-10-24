using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Intacct.Module.Services;

public sealed class IntacctFlowService : FlowServiceBase
{
    protected override string FlowResourcePrefix => "StampliMCP.McpServer.Intacct.Module.Knowledge.flows";

    public IntacctFlowService(
        ILogger<IntacctFlowService> logger,
        IMemoryCache cache,
        FuzzyMatchingService fuzzyMatcher)
        : base(logger, cache, fuzzyMatcher, typeof(IntacctFlowService).Assembly)
    {
    }
}
