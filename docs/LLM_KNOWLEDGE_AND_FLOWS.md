# Knowledge And Flows

This explains the embedded knowledge model (categories, operations, flows) and how tools use it.

## Categories
- File: `Knowledge/categories.json` (per ERP module assembly)
- Shape: `{ "categories": [ { "name": string, "count": int, "description": string }, ... ] }`
- Loader: `StampliMCP.Shared/Services/KnowledgeServiceBase.cs:76`

## Operations
- Files: `Knowledge/operations/<category>.json`
- Shape: `{ "operations": [...] }` or `{ "operations": { "opName": { ... }, ... } }`
  - If an object entry has `operationName` but no `method`, loader injects `method`.
- Loader: `KnowledgeServiceBase.cs:101`
- Lookup:
  - Per-category cache, plus a module-wide index (method → Operation).
  - `FindOperationAsync` and `GetOperationByMethodAsync` scan/index lazily.

## Flows
- Files: `Knowledge/flows/*.json`
- Loader: `StampliMCP.Shared/Services/FlowServiceBase.cs`
  - Lists flow names (`GetAllFlowNamesAsync`) and loads docs (`GetFlowAsync`).
  - Builds op→flow and flow→ops indexes from `usedByOperations`.
- Expected keys (used by tools):
  - `description: string`
  - `usedByOperations: string[]`
  - `constants: object` (nested objects; value/file/line/purpose)
  - `validationRules: string[]`
  - `codeSnippets: object` (name → code)
  - `anatomy: object` (flow/validation/mapping/extra)
  - `criticalFiles: Array<{ file, lines?, purpose?, keyPatterns[]? }>`

## Matching (No Vectors)
- Fuzzy matching: `FuzzyMatchingService` (Fastenshtein), optimal pattern (one query → many patterns).
- Smart parsing: `SmartFlowMatcher` uses `SearchValues` and aliases for fast action/entity extraction.
- Per-ERP synonyms: `Knowledge/matching.json` loaded via `MatchingConfigurationProvider`.
- Flow signatures: `Knowledge/flow-signatures.json` loaded via `FlowSignatureProvider`.
- Recommendation uses dual-signal scoring in `AcumaticaFlowService.MatchFeatureToFlow` (action/entity/keywords).

## Kotlin Golden Reference (Fallback)
- Tool: `get_kotlin_golden_reference` (Acumatica module) will return an embedded reference if local files are missing.
- Embedded source JSON: `Knowledge/kotlin-golden-reference.json` (module-specific); patterns also surfaced.

## Querying Knowledge Effectively
- `erp__query_knowledge(erp, query, scope?)`
  - `scope` is case-insensitive; accepts `operations | flows | constants | all`.
  - If scope is missing/invalid, server may elicit a choice.
  - For broad results, server may elicit `refine` and optional `scope` again.

## Validating Content
- `mcp__validate_embedded_knowledge()` checks for common issues:
  - category count mismatches, duplicate operation methods, unknown operations referenced by flows, missing descriptions.
- `erp__check_knowledge_files(erp)` lists actual embedded resource names.

---
## JSON Shapes (Examples)
- operations (array form)
```
{
  "operations": [
    {
      "method": "exportVendor",
      "summary": "Create/Export new vendor",
      "category": "vendors",
      "requiredFields": { "VendorID": {"type": "string", "maxLength": 30 } },
      "flowTrace": [ { "layer": "service", "file": "...", "lines": "100-140" } ]
    }
  ]
}
```
- operations (object form)
```
{
  "operations": {
    "exportVendor": {
      "operationName": "exportVendor",
      "summary": "Create/Export new vendor",
      "category": "vendors"
    }
  }
}
```
- flow (abridged)
```
{
  "description": "Export purchase orders to Acumatica...",
  "usedByOperations": ["exportPurchaseOrder"],
  "constants": { "MAX_VENDORID_LENGTH": {"value": 30, "file": "...", "line": 42, "purpose": "id length" } },
  "validationRules": ["VendorID: required; max 30"],
  "codeSnippets": { "serialize": "..." },
  "criticalFiles": [ {"file": "...", "lines": "200-260", "purpose": "mapping"} ]
}
```

## Authoring Tips (for Devs)
- Keep `categories.json` counts in sync; the validator will flag mismatches.
- Prefer the array form for operations going forward.
- Name flows in lowercase snake case: `vendor_export_flow`, `standard_import_flow`.
- Ensure `usedByOperations` are real operation methods; the validator warns otherwise.

## Synonyms & Signatures (Per‑ERP)
- `Knowledge/matching.json`: action/entity words + alias map
- `Knowledge/flow-signatures.json`: expected actions/entities/keywords per flow for better ranking.

## How Tools Use This
- `erp__get_flow_details` reads constants/validation/snippets/files and turns them into a navigable summary with NextActions.
- `erp__validate_request` parses flow rules and constants to enforce required fields and max lengths.
- `erp__recommend_flow` uses signatures and synonyms to rank flows; elicitation augments when ambiguous.
