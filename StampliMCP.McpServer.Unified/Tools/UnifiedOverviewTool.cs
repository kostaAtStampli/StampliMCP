using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Unified.Services;
using StampliMCP.Shared.Erp;

namespace StampliMCP.McpServer.Unified.Tools;

[McpServerToolType]
public static class UnifiedOverviewTool
{
    private const string ServerName = "stampli-unified";
    private const string ServerVersion = "1.0.0-alpha";
    private const string PublishPath = "StampliMCP.McpServer.Unified/bin/Release/net10.0/win-x64/publish/stampli-mcp-unified.exe";

    [McpServerTool(
        Name = "mcp_overview",
        Title = "Unified MCP Architecture Guide",
        UseStructuredContent = true)]
    [Description(@"
START HERE - Overview of the unified Stampli MCP architecture.

Highlights:
✓ Single executable hosts every ERP module (Acumatica, Intacct stubs, future ERPs)
✓ Shared tool namespace (`erp__*`) with ERP parameter routing through ErpRegistry
✓ Library-style ERP modules embed their own knowledge, flows, and diagnostics
✓ Self-contained publish artifact for Windows (`stampli-mcp-unified.exe`)

Use this tool when:
• You need a quick refresher on how the unified server is structured
• You are onboarding another ERP module or stub
• You want to sanity-check the available ERP modules and capabilities
• You are wiring automation or CLI configs targeting the unified host
")]
    public static CallToolResult Execute(ErpRegistry registry)
    {
        var erps = registry.ListErps()
            .Select(d => new
            {
                key = d.Key,
                aliases = d.Aliases,
                version = d.Version,
                description = d.Description,
                capabilities = ExpandCapabilities(d.Capabilities)
            })
            .OrderBy(d => d.key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var overview = new
        {
            title = "Stampli Unified MCP Server - Architecture Guide",
            summary = "Single host executable that composes ERP-specific knowledge modules behind one tool surface.",
            server = new
            {
                name = ServerName,
                version = ServerVersion,
                binary = PublishPath,
                toolNamespace = "erp__*",
                registryTool = "erp__list_erps",
                healthTool = "erp__health_check",
                logDirectory = "%TEMP%/mcp_logs/unified"
            },
            erpModules = erps,
            architecture = new
            {
                flow = new[]
                {
                    "StampliMCP.Shared supplies base abstractions (IErpModule, IErpFacade, KnowledgeServiceBase).",
                    "Each ERP lives in a `StampliMCP.McpServer.<Erp>.Module` library that embeds knowledge + services.",
                    "The unified host (`StampliMCP.McpServer.Unified`) registers modules, exposes tools, and routes requests via ErpRegistry."
                },
                extensibility = new[]
                {
                    "Add new ERP by scaffolding `<Erp>Module` library, dropping JSON knowledge under `Knowledge/`, and implementing services.",
                    "Call `registry.Register(..)` once the module is referenced and expose aliases for ergonomics.",
                    "All top-level tools are already ERP-aware—just provide knowledge + flow services."
                },
                stubSupport = new
                {
                    description = "Stub modules (e.g., Intacct) can ship minimal knowledge to validate wiring before real data exists.",
                    recommendedFiles = new[]
                    {
                        "Knowledge/categories.json",
                        "Knowledge/operations/general.json",
                        "Knowledge/flows/<optional>.json"
                    }
                }
            },
            workflows = new
            {
                addErp = new[]
                {
                    "1. Scaffold `StampliMCP.McpServer.<Erp>.Module` (library, not exe).",
                    "2. Implement module class deriving from IErpModule, register knowledge/flow services.",
                    "3. Embed stub knowledge JSON so `erp__query_knowledge` has data on day one.",
                    "4. Reference the module from the unified host project and register it in `Program.cs`.",
                    "5. Publish unified server (`dotnet publish StampliMCP.McpServer.Unified -c Release -r win-x64 --self-contained true`).",
                    "6. Update MCP client configs (e.g., `.claude/settings.local.json`) to point at the published unified exe."
                },
                maintain = new[]
                {
                    "Validate module health with `erp__list_erps` and `erp__health_check`.",
                    "Search knowledge via `erp__query_knowledge` to confirm new content is embedded.",
                    "Surface flows with `erp__list_flows` / `erp__get_flow_details` before releasing guidance.",
                    "Use `erp__validate_request` / `erp__diagnose_error` once validation hooks are implemented."
                }
            },
            keyTools = new[]
            {
                new { name = "erp__list_erps", purpose = "Inventory registered ERP modules and capability flags." },
                new { name = "erp__query_knowledge", purpose = "Search operations/flows per ERP (requires `erp` argument)." },
                new { name = "erp__recommend_flow", purpose = "Flow recommendations leveraging shared intelligence services." },
                new { name = "erp__validate_request", purpose = "ERP-specific payload validation (where implemented)." },
                new { name = "erp__diagnose_error", purpose = "Error triage pipelines tied to ERP knowledge (Acumatica ready)." }
            },
            deployment = new
            {
                publishCommand = "dotnet publish StampliMCP.McpServer.Unified -c Release -r win-x64 --self-contained true",
                output = PublishPath,
                notes = new[]
                {
                    "Ensure MCP clients reference the unified binary (not the legacy Acumatica exe).",
                    "Preview SDK warning (NETSDK1057) is benign; upgrade when .NET 10 GA drops.",
                    "Artifacts include embedded knowledge from every referenced ERP module."
                }
            },
            nextActions = new List<object>
            {
                new { step = "Discover registered ERPs", action = "tool", name = "erp__list_erps", args = (string?)null },
                new { step = "Browse Acumatica knowledge", action = "tool", name = "erp__query_knowledge", args = "erp=acumatica&query=*" },
                new { step = "Browse Intacct stub knowledge", action = "tool", name = "erp__query_knowledge", args = "erp=intacct&query=*" },
                new { step = "Check server health", action = "tool", name = "erp__health_check", args = (string?)null },
                new { step = "Docs: Start Here", action = "doc", name = "docs/START_HERE.md", args = (string?)null },                new { step = "Docs: Architecture", action = "doc", name = "docs/ARCHITECTURE.md", args = (string?)null },                new { step = "Docs: Tools & Schemas", action = "doc", name = "docs/TOOLS_AND_SCHEMAS.md", args = (string?)null },                new { step = "Docs: Knowledge & Flows", action = "doc", name = "docs/KNOWLEDGE_AND_FLOWS.md", args = (string?)null },                new { step = "Docs: Developer Guide", action = "doc", name = "docs/DEVELOPER_GUIDE.md", args = (string?)null },                new { step = "Docs: Manager Brief", action = "doc", name = "docs/MANAGER_BRIEF.md", args = (string?)null }
            }
        };

        var result = new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToNode(overview)
        };

        var json = JsonSerializer.Serialize(overview, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        result.Content.Add(new TextContentBlock { Type = "text", Text = json });

        result.Content.Add(new ResourceLinkBlock
        {
            Uri = "mcp://stampli-unified/erp__list_erps",
            Name = "List registered ERPs"
        });

        if (erps.Any(e => string.Equals(e.key, "acumatica", StringComparison.OrdinalIgnoreCase)))
        {
            result.Content.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=*",
                Name = "Explore Acumatica knowledge"
            });
        }

        if (erps.Any(e => string.Equals(e.key, "intacct", StringComparison.OrdinalIgnoreCase)))
        {
            result.Content.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/erp__query_knowledge?erp=intacct&query=*",
                Name = "Explore Intacct knowledge"
            });
        }

        // Canonical docs quick links
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/START_HERE.md", Name = "Docs: Start Here", Description = "Orientation for humans and LLMs" });
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/ARCHITECTURE.md", Name = "Docs: Architecture", Description = "Unified host + modules (code refs)" });
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/TOOLS_AND_SCHEMAS.md", Name = "Docs: Tools & Schemas", Description = "Catalog + structured outputs" });
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/KNOWLEDGE_AND_FLOWS.md", Name = "Docs: Knowledge & Flows", Description = "JSON shapes and usage" });
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/DEVELOPER_GUIDE.md", Name = "Docs: Developer Guide", Description = "Build, publish, extend, test" });
        result.Content.Add(new ResourceLinkBlock { Uri = "docs/MANAGER_BRIEF.md", Name = "Docs: Manager Brief", Description = "Value, KPIs, roadmap" });

        return result;
    }

    private static IReadOnlyList<string> ExpandCapabilities(ErpCapability capabilities)
    {
        if (capabilities == ErpCapability.None)
        {
            return Array.Empty<string>();
        }

        var flags = Enum.GetValues<ErpCapability>()
            .Where(flag => flag != ErpCapability.None && capabilities.HasFlag(flag))
            .Select(flag => flag.ToString())
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return flags;
    }
}
