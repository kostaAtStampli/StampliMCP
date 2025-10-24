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
- `erp__health_check()` → Server status + registered ERPs.
- `erp__query_knowledge(erp="acumatica", query="vendor", scope="operations")` → Browse operation catalog.
- `erp__query_knowledge(erp="acumatica", query="purchase order", scope="flows")` → Flow catalog with constants/rules.
- `erp__recommend_flow(erp="acumatica", useCase="export vendors")` → Flow anatomy + guidance.

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
- `erp__knowledge_update_plan(erp, mode="files")` → Lists embedded knowledge assets.
- `erp__knowledge_update_plan(erp, mode="validate")` → Validates categories/operations/flows linkage.

## Where To Read Next
- Architecture: `docs/LLM_ARCHITECTURE.md`
- Tools catalog and schemas: `docs/LLM_TOOLS_AND_SCHEMAS.md`
- Knowledge and flows: `docs/LLM_KNOWLEDGE_AND_FLOWS.md`
- Developer guide: `docs/DEVELOPER_GUIDE.md`
- Manager brief: `docs/MANAGER_BRIEF.md`

---
## Common Scenarios
- Explore an ERP module
  1) `erp__query_knowledge(erp="acumatica", query="vendor", scope="operations")`
  2) `erp__query_knowledge(erp="acumatica", query="vendor", scope="flows")`
  3) `erp__recommend_flow(erp="acumatica", useCase="vendor export")` for anatomy/constants/validation
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
  - Check client logs for `initialize.capabilities.elicitation`.
  - Server continues without prompts when the client declines; refine manually if needed.
- Flow not found
  - Run `erp__query_knowledge(erp, query="*", scope="flows")` to confirm the flow name.
- Knowledge mismatch
  - Run `erp__knowledge_update_plan(erp, mode="validate")`; rebuild if knowledge was edited.

## Tips
- Use explicit `erp` everywhere—tools are unified.
- Scope your knowledge query to reduce noise: `scope="flows"` or let the prompt guide you.
- Follow NextActions links embedded in tool results to move faster.
