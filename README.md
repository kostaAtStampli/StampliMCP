# Stampli MCP – Unified Server

A single MCP host that serves Stampli’s ERP knowledge base across multiple modules (Acumatica, Intacct, …).  Each ERP implements a library containing its embedded knowledge, services, and optional validation/diagnostics.  `StampliMCP.McpServer.Unified` discovers those libraries and exposes a consistent set of `erp__*` tools for clients.

---
**Server:** `stampli-mcp-unified`
**Modules:** Acumatica (full), Intacct (stub)
**Targets:** .NET 10 (preview SDK)
**Docs Index:** See DOCS.md
---

## Quick Start

### 1. Build (Debug)
```bash
# From repo root
"/mnt/c/Program Files/dotnet/dotnet.exe" build StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj -c Debug --nologo
```

### 2. Run Locally
```bash
# Launch unified host
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj
```

### 3. Configure MCP Client (example: Claude Desktop)
```json
{
  "mcpServers": {
    "stampli-unified": {
      "command": "C:\\Users\\YourName\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Unified\\bin\\Debug\\net10.0\\stampli-mcp-unified.dll",
      "args": []
    }
  }
}
```

### 4. Call Tools
```javascript
// List registered ERPs
mcp__erp__list_erps()

// Query knowledge
mcp__erp__query_knowledge({ erp: "acumatica", query: "vendor" })

// Validate a request (Acumatica module implements validation)
mcp__erp__validate_request({ erp: "acumatica", operation: "exportVendor", requestPayload: "{\"VendorID\":\"123\"}" })
```

## Self-Contained Publish (Release)
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj \
  -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true --nologo
```
Output: `StampliMCP.McpServer.Unified/bin/Release/net10.0/win-x64/publish/stampli-mcp-unified.exe`

## Architecture Overview

```
StampliMCP.McpServer.Unified/
  Program.cs            # Host + registry wiring
  Services/ErpRegistry  # ERP lookup + facade scope
  Tools/erp__*.cs       # Generic tools (knowledge, flows, validation…)

Modules/
  StampliMCP.McpServer.Acumatica.Module/
    Knowledge/          # Embedded JSON/MD/XML resources
    Services/           # ERP-specific services (knowledge, flows, validation…)
    Tools/              # Acumatica developer helpers
  StampliMCP.McpServer.Intacct.Module/
    Knowledge/          # Stub data for multi-ERP verification
    Services/           # Thin wrappers over shared base classes
```

- `IErpModule` registers services and exposes metadata/capabilities.
- `IErpFacade` creates a scoped view per tool call (solves DI lifetime issues).
- Generic tools resolve a facade for the requested ERP and operate on shared models from `StampliMCP.Shared`.

## Adding a New ERP Module

1. `dotnet new classlib -n StampliMCP.McpServer.Foo.Module -f net10.0`
2. Follow the Intacct module template:
   - Create `Knowledge/` with categories, operations, flows.
   - Implement `<Foo>KnowledgeService` / `<Foo>FlowService` inheriting the shared base classes.
   - Implement `<Foo>Module` returning aliases, capabilities, and services.
3. Register the module in `StampliMCP.McpServer.Unified/Program.cs` (add to the `modules` array).
4. Provide optional validation/diagnostic/recommendation services by implementing the shared interfaces (e.g., `IErpValidationService`).
5. Build and run `erp__list_erps` to verify the module loads.

## Tool Surface (Unified)

| Tool | Description |
|------|-------------|
| `erp__health_check` | Unified server status + registered ERPs |
| `erp__list_erps` | Keys, aliases, capabilities |
| `erp__query_knowledge` | Natural-language search over operations/flows (scope is case-insensitive; may prompt for scope/refinement if client supports elicitation) |
| `erp__list_operations` | ERP operations with optional flow info |
| `erp__list_flows` | ERP flows + usedByOperations |
| `erp__get_flow_details` | Flow anatomy, constants, validation rules |
| `erp__validate_request` | Pre-flight validation (if ERP module implements it) |
| `erp__diagnose_error` | Error triage (module-provided) |
| `erp__recommend_flow` | Flow recommendation (module-provided; may prompt to choose among alternatives) |
| `erp__knowledge_update_plan` | Plan/apply knowledge updates from PR context (two-scan enforced) |
| `erp__challenge_scan_findings` | Generate Scan‑2 questions from Scan‑1 results (ERP‑agnostic) |
| `erp__list_prompts` | List prompts registered for the ERP module |
| `erp__check_knowledge_files` | List embedded Knowledge resources for the ERP |

Module-specific developer helpers (e.g., Kotlin TDD workflow) remain available and are registered via the module assembly.

### Acumatica Helper Tools

- `get_kotlin_golden_reference` – loads the Kotlin exportVendor reference bundle (mandatory before Kotlin TDD workflows). Falls back to embedded reference if local files are unavailable.
- `kotlin_tdd_workflow` – prescriptive Kotlin implementation planner with enforced dual-file scan.
- `acumatica__add_knowledge_from_pr_prompt` – prompt-only helper used historically for planning knowledge updates (replaced by `erp__knowledge_update_plan`).

### Knowledge Update Planner

- Use `erp__knowledge_update_plan(erp, prNumber?, learnings?, currentBranch?, dryRun=true)`
  - Captures git context and ERP knowledge snapshot
  - Requires STRICT JSON verdict; two-scan enforcement via `erp__challenge_scan_findings`
  - When `dryRun=false` and schema valid, safely applies updates limited to `…/Module/Knowledge/**`
  - Configure CLI via env (optional):
    - `CLAUDE_CLI_PATH` (default `~/.local/bin/claude`)
    - `CLAUDE_CLI_ARGS` (default `--print --dangerously-skip-permissions`)

## Scripts & Shortcuts

- `mcp-runner.cmd`, `mcp-wrapper.py`, `stampli-mcp-wrapper.sh`, `test-mcp-manual.sh`: all point at the unified host.
- Aspire AppHost (`StampliMCP.AppHost`) orchestrates API, web UI, and unified MCP for local multi-service runs.

## Testing

`StampliMCP.E2E` runs end‑to‑end tests against the unified server over stdio.

Examples:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build StampliMCP.E2E -c Debug --nologo
"/mnt/c/Program Files/dotnet/dotnet.exe" test StampliMCP.E2E -c Debug --nologo
```
Planner tests can be enabled with a real Claude CLI by setting `CLAUDE_CLI_PATH`; otherwise they can use a local stub for deterministic runs.

## Status

- Acumatica module fully ported (knowledge, validation, developer tools).
- Intacct module provides a stub dataset proving the multi-ERP wiring.
- Ready to onboard additional ERPs by cloning the module template.
