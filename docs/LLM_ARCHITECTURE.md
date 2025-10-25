# Architecture

- Unified host: single MCP server (`stampli-mcp-unified.exe`) composing ERP modules.
- Routing: `ErpRegistry` normalizes ERP key/aliases and returns a scoped facade per call.
- Shared services: `KnowledgeServiceBase` and `FlowServiceBase` load embedded resources; `FuzzyMatchingService` handles string matching.
- Elicitation: typed `ElicitRequestParams` via `IMcpServer` extension; helper at `StampliMCP.McpServer.Unified/Services/ElicitationCompat.cs:27`.
- Structured outputs: tools set `UseStructuredContent=true` and return `CallToolResult.StructuredContent` with JSON nodes.

## Components (Code References)
- Host bootstrap: `StampliMCP.McpServer.Unified/Program.cs`
  - Registers modules (Acumatica, Intacct), tools, prompts, resources.
  - MemoryCache tuned (size 2000, compaction 0.2), logs to `%TEMP%/mcp_logs/unified`.
- ERP registry/facade: `StampliMCP.McpServer.Unified/Services/ErpRegistry.cs`
  - Case-insensitive keys, alias normalization, scoped `IErpFacade` per tool call.
- Knowledge loading: `StampliMCP.Shared/Services/KnowledgeServiceBase.cs`
  - Reads `Knowledge/categories.json` and `Knowledge/operations/<category>.json` (array or object format), caches and indexes operations.
- Flow loading: `StampliMCP.Shared/Services/FlowServiceBase.cs`
  - Reads `Knowledge/flows/*.json`, lists names, loads JSON, builds op→flow and flow→ops indexes.
- Resource catalog: `StampliMCP.McpServer.Unified/Services/UnifiedResourceCatalog.cs`
  - Feeds `resources/list` and `resources/read` with stable URIs (`mcp://stampli-unified/erp/{erp}/flows[/<flow>]`) and renders JSON + Markdown responses.
- Matching/intelligence:
  - Fuzzy: `StampliMCP.Shared/Services/FuzzyMatchingService.cs`
  - Smart matcher: `StampliMCP.McpServer.Acumatica.Module/Services/SmartFlowMatcher.cs` (SearchValues, aliases, typo distance).
  - Per-ERP matching config: `StampliMCP.McpServer.Acumatica.Module/Knowledge/matching.json` loaded by `MatchingConfigurationProvider`.
  - Flow signatures + dual-signal scoring: `FlowSignatureProvider` + `AcumaticaFlowService.MatchFeatureToFlow()`.
- Module registration (Acumatica): `StampliMCP.McpServer.Acumatica.Module/AcumaticaModule.cs`
  - Registers knowledge/flow/validation/diagnostic/recommendation services, and matching/signature providers.

## Elicitation (Server)
- Helper builds typed schema and calls `server.ElicitAsync(request, ct)`.
- Used in:
  - `erp__query_knowledge` to pick scope/refine.
  - `erp__recommend_flow` to disambiguate low-confidence picks.
  - `erp__validate_request` to auto-suggest patched payloads.
  - `erp__diagnose_error` to capture context when causes are ambiguous.

## Flows/Knowledge Embedding
- ERP modules embed knowledge assets under `Knowledge/**`.
- Unified host references module assemblies so assets are baked into the final exe.

## Observability
- Logs include fuzzy matching, flow loads, initialization, elicitation actions (Debug).
- `erp__knowledge_update_plan(erp, mode="validate")` verifies categories/operations/flows linkage.

---
## Request Flow (End‑to‑End)
- Client → Unified host (stdio MCP)
- Host → `ErpRegistry.Normalize(erp)` → scoped `IErpFacade`
- Tool executes with facade services (Knowledge/Flow/Fuzzy/Validation/Diagnostic)
- Tool populates `NextActions` with resource links; clients can hit `resources/read` to fetch the detailed flow doc without rerunning the tool.
- Tool may call `server.ElicitAsync()` to gather inputs (optional)
- Tool returns `CallToolResult` with `StructuredContent` + text + NextActions

## Caching & Indexes
- MemoryCache (size≈2000 entries) caches categories, operations per category, and flows.
- `KnowledgeServiceBase` also keeps an in‑memory `method → Operation` index for fast lookups.
- `FlowServiceBase` builds `operation → flow` and `flow → operations` maps from `usedByOperations`.
- All caches expire on a sliding basis (10 minutes) and are rebuilt lazily.

## Scaling To 15 ERPs
- Each ERP ships its own `Knowledge/**` (categories, operations, flows, optional synonyms/signatures).
- The host only wires modules; no cross‑module coupling.
- Matching is per‑ERP configurable via `Knowledge/matching.json` and `Knowledge/flow-signatures.json`.
- Knowledge schema validation tool (`erp__knowledge_update_plan(erp, mode="validate")`) guards drift.

## SDK Notes (Verified)
- Elicitation uses typed `ElicitRequestParams` via `IMcpServer` (SDK 0.4.0‑preview.3). Code: `ElicitationCompat.TryElicitAsync`.
- `IMcpServer` is marked obsolete in preview; migration path is `McpServer` when supported.

## Logging
- Console (stderr) and `%TEMP%/mcp_logs/unified/structured.jsonl` (compact JSON) include:
  - Initialization, module registry, fuzzy matches, flow loads, elicitation actions (Debug).

## Safety
- Knowledge update tool applies guarded edits limited to `…/Module/Knowledge/**`.
- Tools never shell out on their own; external CLIs are opt‑in and validated.
