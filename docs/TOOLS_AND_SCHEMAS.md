# Tools And Schemas

This is the canonical tool catalog and the structured results the server returns. All tool names and parameters are verified from code in `StampliMCP.McpServer.Unified/Tools`.

## Core Tools (Unified)
- `erp__list_erps()`
  - File: `ErpHealthTools.cs:14`
  - Returns: registered ERPs with aliases, capability flags, version.
- `erp__health_check()`
  - File: `ErpHealthTools.cs:33`
  - Returns: server status + ERP summary.
- `mcp_overview()`
  - File: `UnifiedOverviewTool.cs:17`
  - Returns: architecture overview and next actions.
- `mcp__debug_elicitation()`
  - File: `ElicitationDebugTool.cs:14`
  - Purpose: Probe client support for elicitation; returns supported/action/content.
- `mcp__validate_embedded_knowledge()`
  - File: `KnowledgeValidationTool.cs`
  - Purpose: Sanity-check embedded categories/operations/flows per ERP.

## Knowledge/Flow Tools
- `erp__list_operations(erp)` → ops per category, with optional flow mapping
  - File: `ErpKnowledgeTools.cs:16`
- `erp__list_flows(erp)` → list all flows; name, description, usedByOperations
  - File: `ErpKnowledgeTools.cs:60`
- `erp__get_flow_details(erp, flow)` → flow anatomy/constants/validationRules/codeSnippets/criticalFiles
  - File: `ErpFlowDetailsTool.cs:15`
- `erp__query_knowledge(erp, query, scope?)`
  - File: `ErpKnowledgeTools.cs:96`
  - Scope is case-insensitive; if missing/invalid, the server may elicit a scope.
  - Returns: operations, flows, constants, validationRules, code examples, NextActions.

## Intelligence/Recommendation/Validation
- `erp__recommend_flow(erp, useCase)`
  - File: `ErpRecommendationTool.cs:18`
  - Behavior: uses ERP recommendation service; may elicit clarification on low confidence.
  - Returns FlowRecommendation with score breakdown.
- `erp__validate_request(erp, operation, payload)`
  - File: `ErpValidationTool.cs:16`
  - Behavior: validates JSON against flow rules; may elicit “auto-fix” to propose a SuggestedPayload.
- `erp__diagnose_error(erp, errorMessage)`
  - File: `ErpDiagnosticTool.cs:14`
  - Behavior: diagnoses; may elicit context (operation/stage/recentChanges) if ambiguous.

## Knowledge Maintenance
- `erp__knowledge_update_plan(erp, prNumber?, learnings?, currentBranch?, dryRun=true)`
  - File: `ErpKnowledgeUpdateTool.cs:16`
  - Planner for PR-based knowledge updates; guarded to Knowledge/**.
- `erp__challenge_scan_findings(scan1Results, challengeAreas[])`
  - File: `ErpChallengeScanFindingsTool.cs:10`
  - Generates mandatory second-scan questions.
- `erp__check_knowledge_files(erp)`
  - File: `ErpKnowledgeFilesTool.cs:12`
  - Lists embedded knowledge resources per ERP.
- `erp__list_prompts(erp)`
  - File: `ErpPromptTools.cs:20`
  - Lists module prompts available.

## Structured Results (Schemas)
- FlowRecommendation (file: `StampliMCP.Shared/Models/StructuredResults.cs:116`)
  - `Summary: string?`
  - `FlowName: string`
  - `Confidence: double (0–1)`
  - `Reasoning: string`
  - `Details: FlowDetail`
  - `AlternativeFlows: List<AlternativeFlow>`
  - `Scores: Dictionary<string,double>` (overall/action/entity/keywords)
  - `NextActions: List<ResourceLinkBlock>`
- ValidationResult (file: `StructuredResults.cs`)
  - `IsValid: bool`
  - `Operation: string`
  - `Flow: string`
  - `Errors: List<ValidationError>`
  - `Warnings: List<string>`
  - `AppliedRules: List<string>`
  - `Suggestions: List<string>`
  - `SuggestedPayload?: string` (when auto-fix accepted)
  - `NextActions: List<ResourceLinkBlock>`
- ErrorDiagnostic (file: `StructuredResults.cs`)
  - `Summary?: string`
  - `ErrorMessage: string`
  - `ErrorCategory: string`
  - `PossibleCauses: List<string>`
  - `Solutions: List<ErrorSolution>`
  - `RelatedFlowRules: List<string>`
  - `PreventionTips: List<string>`
  - `AdditionalContext?: Dictionary<string,string>` (from elicitation)
  - `NextActions: List<ResourceLinkBlock>`

## Elicitation Contracts (Server → Client)
- The server asks for primitive inputs via schemas:
  - BooleanSchema, NumberSchema, StringSchema, EnumSchema (subset)
- Helper code: `StampliMCP.McpServer.Unified/Services/ElicitationCompat.cs:27`
- Examples in tools: `ErpKnowledgeTools`, `ErpRecommendationTool`, `ErpValidationTool`, `ErpDiagnosticTool`.


---
## Example Calls
- Recommend flow
```
erp__recommend_flow(erp="acumatica", useCase="sync purchase orders")
```
Returns (abridged):
```
{
  "FlowName": "export_po_flow",
  "Confidence": 0.78,
  "Reasoning": "entities po, actions export, keywords order",
  "Scores": { "overall": 0.78, "entity": 1.0, "action": 0.66, "keywords": 0.5 },
  "AlternativeFlows": [ { "Name": "po_matching_flow", "Confidence": 0.52 } ],
  "NextActions": [ ... ]
}
```

- Validate request (with auto‑fix)
```
erp__validate_request(erp="acumatica", operation="exportVendor", payload="{...}")
```
If required fields are missing and you accept auto‑fix, result includes:
```
{
  "IsValid": false,
  "Errors": [ ... ],
  "SuggestedPayload": "{
  "VendorID": "<TODO: provide VendorID>", ... }",
  "NextActions": [ { "Name": "Re-run validation with SuggestedPayload" } ]
}
```

- Knowledge query (with scope prompt)
```
erp__query_knowledge(erp="acumatica", query="purchase orders")
```
Client may receive an elicitation form to choose scope (`operations|flows|constants|all`).

## Elicitation Example
Server request (conceptual):
```
Message: "Refine your search"
RequestedSchema: {
  Properties: {
    scope: EnumSchema { Description: "Choose: operations | flows | constants | all", Enum: ["operations","flows","constants","all"] },
    refine: StringSchema { Description: "Extra keywords (optional)" }
  }
}
```
Client response (accepted):
```
Action: "accept",
Content: { scope: "flows", refine: "export" }
```

## Error Handling Contracts
- Tools never throw exceptions outward; errors are represented in result objects (`Errors`, `Solutions`, `Summary`).
- Invalid inputs return structured errors (e.g., `ValidationResult.Errors` with `rule` and `expected`).
