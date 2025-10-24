using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Intacct.Module.Services;

public sealed class IntacctKnowledgeService : KnowledgeServiceBase
{
    public IntacctKnowledgeService(ILogger<IntacctKnowledgeService> logger, IMemoryCache cache)
        : base(logger, cache, Assembly.GetExecutingAssembly())
    {
    }

    protected override string ResourcePrefix => "StampliMCP.McpServer.Intacct.Module.Knowledge";

    protected override Dictionary<string, string> CategoryFileMapping => new()
    {
        ["general"] = "operations/general.json"
    };
}
