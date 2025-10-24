# Stampli MCP – Unified Context

You are talking to `stampli-mcp-unified`, a single MCP server that exposes multiple ERP modules.  Always include the `erp` parameter when calling generic tools.

## Registered ERPs (currently)
- `acumatica` – full knowledge, flows, validation, developer helpers
- `intacct` – stub module to validate multi-ERP wiring

Call `erp__list_erps()` if you need the live list and capability flags.

## Core Tools
- `erp__query_knowledge(erp, query, scope?)` (scope is case-insensitive: operations | flows | constants | all; may prompt to pick scope/refine if supported)
- `erp__list_operations(erp)`
- `erp__list_flows(erp)`
- `erp__get_flow_details(erp, flow)`
- `erp__validate_request(erp, operation, payload)`
- `erp__diagnose_error(erp, errorMessage)`
- `erp__recommend_flow(erp, useCase)` (may prompt to choose among alternatives if confidence is low)
- `erp__knowledge_update_plan(erp, prNumber?, learnings?, currentBranch?, dryRun=true)`
- `erp__challenge_scan_findings(scan1Results, challengeAreas[])`
- `erp__health_check()` / `erp__list_erps()`

Module-specific helpers (e.g., Kotlin TDD workflow, knowledge authoring) are still available—call `erp__list_prompts(erp)` to discover. Note: `get_kotlin_golden_reference` falls back to embedded reference when local files aren’t present.

Tip: For the knowledge planner, you can set `CLAUDE_CLI_PATH` and `CLAUDE_CLI_ARGS` environment variables to control the CLI invocation used by the server when generating plans.

## Knowledge Layout
- Module knowledge is embedded (see `StampliMCP.McpServer.<Erp>.Module/Knowledge`).
- Categories + operations + flows follow the shared JSON schema in `StampliMCP.Shared`.

## Development
- Use the module template (compare Acumatica vs Intacct) when adding new ERPs.
- Shared services and models live in `StampliMCP.Shared`.
- Unified host: `StampliMCP.McpServer.Unified`.
