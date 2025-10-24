# Manager Brief

## What This Server Does
- Exposes a unified, AI-friendly interface to ERP-specific knowledge and tools.
- Accelerates developer productivity (and model accuracy) by serving structured guidance, validation, and diagnostics.
- Supports multiple ERPs in a single binary; each module ships its own embedded knowledge.

## Why It Matters
- Faster feature delivery: LLMs and devs get the right flow, rules, and next steps immediately.
- Fewer integration mistakes: Pre-flight validation and clear error diagnostics reduce rework.
- Scales cleanly: Adding a new ERP is a library + knowledge drop; unified host composes them.

## Key Capabilities
- Knowledge search over operations/flows (with scope prompts when needed)
- Flow recommendation with transparent confidence and score breakdown
- Request validation (with optional auto-fix suggestions)
- Error diagnostics (with context elicitation when ambiguous)
- Guarded knowledge updates tied to PRs

## Elicitation (Interactive Prompts)
- The server can ask targeted questions when the request is ambiguous.
- Measured benefit: higher first-try success, fewer clarification loops.

## KPIs To Track
- First-try flow selection rate
- Elicitation acceptance rate and success lift
- Time-to-first-useful-result from tool invocation
- Validation auto-fix application rate

## Operational Notes
- Single self-contained Windows binary for deployment
- Logs to `%TEMP%/mcp_logs/unified/structured.jsonl`
- Preview SDK warning is expected; migration planned when GA lands

## Roadmap Highlights
- Expand synonyms/signatures per ERP to improve matching recall
- Add CI knowledge schema validation gates
- Migrate to `McpServer` API when available
