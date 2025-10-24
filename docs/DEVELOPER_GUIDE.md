# Developer Guide

This is the single source of truth for building, publishing, extending, and testing the Unified MCP.

## Prereqs
- .NET SDK 10 preview (NETSDK1057 warnings expected)
- Windows for self-contained publish target `win-x64`
- Git, and an MCP client (Cursor/Claude) for local verification

## Build & Publish
- Build all:
```
dotnet build -c Release
```
- Publish unified exe (self-contained, single file):
```
dotnet publish StampliMCP.McpServer.Unified -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
- Binary:
```
StampliMCP.McpServer.Unified/bin/Release/net10.0/win-x64/publish/stampli-mcp-unified.exe
```
- Kill prior process if necessary (Windows):
```
taskkill /F /IM stampli-mcp-unified.exe
```

## Logs & Debug
- Logs: `%TEMP%/mcp_logs/unified/structured.jsonl` (compact JSON) and console stderr.
- Debug elicitation support quickly with `mcp__debug_elicitation()`.
- Validate knowledge with `mcp__validate_embedded_knowledge()` before publishing.

## Add A New ERP
1) Create library project `StampliMCP.McpServer.<Erp>.Module`
2) Implement `IErpModule` (Key, Aliases, Capabilities, Descriptor), and `IErpFacade` if needed
3) Register services (KnowledgeServiceBase subclass, FlowServiceBase subclass, optional Validation/Diagnostic/Recommendation)
4) Add knowledge assets under `Knowledge/` and mark as `<EmbeddedResource>` (see Acumatica module csproj)
5) Reference the module from `StampliMCP.McpServer.Unified`, register instance in `Program.cs`
6) Publish unified server and verify tools: `erp__list_erps`, `erp__query_knowledge`, `erp__list_flows`

## Knowledge Authoring
- Files:
  - `Knowledge/categories.json` – registry of categories
  - `Knowledge/operations/<category>.json` – operations per category (prefer array form)
  - `Knowledge/flows/*.json` – flow details (description, usedByOperations, constants, validationRules, codeSnippets, criticalFiles)
  - Optional: `Knowledge/matching.json` (synonyms) and `Knowledge/flow-signatures.json`
- Validation:
  - Run `mcp__validate_embedded_knowledge()` to detect count mismatches, unknown ops, missing descriptions
- Contribution discipline:
  - Keep counts correct; ensure usedByOperations map to real `Operation.Method`
  - Favor short, precise validation rules (`Field: required; max 30`)

## Elicitation & Structured Output
- Use `ElicitationCompat.TryElicitAsync(server, message, fields, ct)` for typed prompts
- Keep requests primitive: Boolean, Number, String, Enum
- Make results structured via `UseStructuredContent=true` and set `StructuredContent = JsonSerializer.SerializeToNode(new { result })`

## Testing (Quick Pointers)
- E2E test harness under `StampliMCP.E2E` spins up the published server and connects an MCP client
- Manual smoke:
  - `erp__list_erps`, `erp__list_flows`, `erp__query_knowledge`
  - `erp__recommend_flow` with an ambiguous use case to trigger elicitation
  - `erp__validate_request` with intentionally missing fields to test SuggestedPayload

## SDK & Migration Notes
- We’re on `ModelContextProtocol` 0.4.0-preview.3; `IMcpServer` is marked obsolete but used with the ElicitAsync extension
- Plan to migrate to `McpServer` API when the SDK stabilizes
