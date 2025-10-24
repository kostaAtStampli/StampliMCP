# Start Here

- Purpose: Quick orientation for humans and LLMs to use Stampli Unified MCP effectively.
- Audience: Anyone connecting a client (Cursor, Claude, etc.) and trying first tools.

## What This Is
- Single executable MCP server that hosts multiple ERP modules behind a unified tool surface (`erp__*`).
- Embedded knowledge (operations, flows, constants, rules) ships inside each ERP module’s assembly.
- Elicitation prompts: server can ask the user for missing info (supported by modern MCP clients).

## Connect
- Binary: `StampliMCP.McpServer.Unified/bin/Release/net10.0/win-x64/publish/stampli-mcp-unified.exe`
- Example Cursor config entry:
  - Name: `stampli-unified`
  - Command: full path to `stampli-mcp-unified.exe`
  - Env: `MCP_DEBUG=true` (optional)

## First Calls To Try
- `mcp_overview()` → High-level architecture and next actions.
- `erp__list_erps()` → Registered ERPs + capabilities.
- `mcp__debug_elicitation()` → Confirms whether your client supports elicitation.
- `erp__list_flows(erp="acumatica")` → Flow catalog for an ERP.
- `erp__query_knowledge(erp="acumatica", query="*")` → Browse categories/operations/flows.

## Elicitation (Prompts)
- The server may ask you to choose a scope (operations | flows | constants | all) or refine ambiguous queries.
- If your client supports elicitation, you’ll see forms with fields like enums/booleans/strings; otherwise the tools fall back gracefully.

## Structured Results
- Tools return structured content (JSON) plus a readable text view.
- Key result types:
  - FlowRecommendation: name, confidence, reasoning, Alternatives, Scores, NextActions.
  - ValidationResult: IsValid, Errors, Warnings, AppliedRules, SuggestedPayload?, NextActions.
  - ErrorDiagnostic: Category, Causes, Solutions, PreventionTips, AdditionalContext?, NextActions.

## Useful Health/Debug
- `erp__health_check()` → Status + ERP summary.
- `erp__check_knowledge_files(erp)` → Lists embedded Knowledge resources for the ERP.
- `mcp__validate_embedded_knowledge()` → Validates categories/operations/flows linkage across ERPs.

## Where To Read Next
- Architecture: `docs/LLM_ARCHITECTURE.md`
- Tools catalog and schemas: `docs/LLM_TOOLS_AND_SCHEMAS.md`
- Knowledge and flows: `docs/LLM_KNOWLEDGE_AND_FLOWS.md`
- Developer guide: `docs/DEVELOPER_GUIDE.md`
- Manager brief: `docs/MANAGER_BRIEF.md`

---
## Common Scenarios
- Explore an ERP module
  1) `erp__list_flows(erp="acumatica")`
  2) `erp__get_flow_details(erp="acumatica", flow="vendor_export_flow")`
  3) `erp__query_knowledge(erp="acumatica", query="vendor export", scope="flows")`
- Find the right flow for a vague ask
  1) `erp__recommend_flow(erp="acumatica", useCase="sync POs")`
  2) If prompted, pick between export/import/matching/bulk
  3) Use NextActions to jump to details or validation
- Validate a request
  1) `erp__validate_request(erp, operation, payload)`
  2) If missing fields, accept auto‑fix → read `SuggestedPayload`
  3) Copy SuggestedPayload, adjust, re‑run validation

## Quick Troubleshooting
- No elicitation form appears
  - Run `mcp__debug_elicitation()` to confirm support.
  - Check client logs for `initialize.capabilities.elicitation`.
- Flow not found
  - Verify name from `erp__list_flows(erp)` and try again.
- Knowledge mismatch
  - Run `mcp__validate_embedded_knowledge()`; rebuild if knowledge was edited.

## Tips
- Use explicit `erp` everywhere—tools are unified.
- Scope your knowledge query to reduce noise: `scope="flows"` or let the prompt guide you.
- Follow NextActions links embedded in tool results to move faster.
