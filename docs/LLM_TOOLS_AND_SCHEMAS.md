# Tools And Schemas

This is the canonical tool catalog and the structured results the server returns. All tool names and parameters are verified from code in `StampliMCP.McpServer.Unified/Tools`.

## Core Tools (Unified)
- `erp__health_check()`
  - File: `ErpHealthTools.cs`
  - Returns server status plus the registered ERPs with aliases, descriptions, and capability flags.
- `erp__query_knowledge(erp, query, scope?)`
  - File: `ErpKnowledgeTools.cs`
  - Normalizes scope (`operations|flows|constants|all`) and will elicit a scope when omitted; returns operations, flows, constants, validation rules, and code examples with next-action links.
- `erp__recommend_flow(erp, useCase)`
  - File: `ErpRecommendationTool.cs`
  - Produces a `FlowRecommendation` with confidence scoring, flow details, and fallback refinement via elicitation.
- `erp__validate_request(erp, operation, payload)`
  - File: `ErpValidationTool.cs`
  - Validates payloads against flow rules; can elicit an auto-fix to populate a SuggestedPayload with placeholders.
- `erp__diagnose_error(erp, errorMessage)`
  - File: `ErpDiagnosticTool.cs`
  - Catalog-driven diagnostics with optional elicitation for extra context; returns causes, solutions, and prevention tips.
- `erp__knowledge_update_plan(erp, prNumber?, learnings?, currentBranch?, dryRun=true, mode="plan")`
  - File: `ErpKnowledgeUpdateTool.cs`
  - Modes: `plan|apply` (Acumatica PR planner), `validate` (embedded knowledge audit), `files` (embedded resource inventory).

### Developer-only (Debug builds)
- `mcp_overview()` – Architecture orientation
- `mcp__debug_elicitation()` – Capability probe
- `erp__list_prompts(erp)` – Module prompts catalog
- Kotlin helpers (`get_kotlin_golden_reference`, `kotlin_tdd_workflow`, `modern_harness_guide`) remain available in DEV builds only.

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
- If the client declines or lacks support, the helper caches it and tools continue without prompting.
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

---
## Full JSON Examples

- FlowRecommendation
```
{
  "Summary": "Recommended flow 'export_po_flow' (HIGH)",
  "FlowName": "export_po_flow",
  "Confidence": 0.86,
  "Reasoning": "entities po, actions export, keywords order",
  "Details": {
    "Name": "export_po_flow",
    "Description": "Export purchase orders to Acumatica with validation...",
    "Anatomy": { "Flow": "...", "Validation": "...", "Mapping": "...", "AdditionalInfo": { } },
    "Constants": { "MAX_VENDORID_LENGTH": { "Name": "MAX_VENDORID_LENGTH", "Value": "30", "File": "...", "Line": 42, "Purpose": "id length" } },
    "ValidationRules": [ "VendorID: required; max 30" ],
    "CodeSnippets": { "serialize": "..." },
    "CriticalFiles": [ { "File": "src/...", "Lines": "200-260", "Purpose": "mapping", "KeyPatterns": ["put("VendorID", ...)"] } ],
    "NextActions": [ ]
  },
  "AlternativeFlows": [ { "Name": "po_matching_flow", "Confidence": 0.52, "Reason": "entity match but action ambiguous" } ],
  "Scores": { "overall": 0.86, "entity": 1.0, "action": 0.8, "keywords": 0.5 },
  "NextActions": [ { "Uri": "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=export_po_flow&scope=flows", "Name": "Search export_po_flow guidance" } ]
}
```

- ValidationResult
```
{
  "IsValid": false,
  "Operation": "exportVendor",
  "Flow": "vendor_export_flow",
  "Errors": [
    { "Field": "VendorID", "Rule": "flow_required_field", "Message": "VendorID is required", "Expected": "Provide a value" }
  ],
  "Warnings": [],
  "AppliedRules": [ "flow_required:VendorID" ],
  "Suggestions": [ "Fix VendorID: Provide a value" ],
  "SuggestedPayload": "{
  "VendorID": "<TODO: provide VendorID>"
}",
  "NextActions": [
    { "Uri": "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=vendor_export_flow&scope=flows", "Name": "Review flow guidance" },
    { "Uri": "mcp://stampli-unified/erp__validate_request", "Name": "Re-run validation with SuggestedPayload" }
  ]
}
```

- ErrorDiagnostic
```
{
  "Summary": "Diagnostics completed for ERP 'acumatica'",
  "ErrorMessage": "Invalid field: VendorID",
  "ErrorCategory": "Validation",
  "PossibleCauses": [ "VendorID missing", "VendorID exceeds max length" ],
  "Solutions": [ { "Description": "Provide VendorID ≤ 30", "CodeExample": "payload.VendorID = 'ACU-123'" } ],
  "RelatedFlowRules": [ "VendorID: required; max 30" ],
  "PreventionTips": [ "Validate payload before sending" ],
  "AdditionalContext": { "operation": "exportVendor", "stage": "export", "recentChanges": "false" },
  "NextActions": [ { "Uri": "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=vendor_export_flow&scope=flows", "Name": "Review flow guidance" } ]
}
```
