# MCP Nuclear 2025 Architecture Guide (v3.0.0)

## Overview

Version 3.0.0 introduces **True Single Entry Point** architecture:
- From 16 tools → **1 main tool** (`kotlin_tdd_workflow`)
- Complete knowledge provided upfront in single response
- Enforced TDD workflow with session management
- Natural conversation flow with query/help system

## Quick Start

### Single Entry Point Flow
```
User: "implement exportVendor operation"

AI uses: kotlin_tdd_workflow(command="start", context="exportVendor")

Returns: Complete knowledge in ONE response:
  - Test templates (full code)
  - Implementation templates (full code)
  - Validation rules with exact error messages
  - File pointers to legacy code
  - Complete tasklist
  - Session ID for continuation
```

### Continue Workflow
```
AI: "I wrote tests, they're failing"

AI uses: kotlin_tdd_workflow(command="continue", context="tests failing", sessionId="tdd_123")

Returns: Implementation guidance + move to GREEN phase
```

## Architecture

```
┌──────────────────────────────────────────┐
│      kotlin_tdd_workflow (SINGLE)        │
│                                          │
│  Commands:                               │
│  - start    → Begin new feature         │
│  - continue → Progress through phases    │
│  - query    → Get help                   │
│  - list     → Show all operations        │
└──────────────────────────────────────────┘
              ↓
   [Session Management: 30min timeout]
              ↓
      RED → GREEN → REFACTOR → COMPLETE
```

**Supporting Diagnostic Tool:**
- `health_check` - Server status and diagnostics

## Tool Specification

### kotlin_tdd_workflow (SINGLE ENTRY POINT)

**Purpose**: Single tool managing complete TDD workflow from discovery to completion

**Input**:
```typescript
{
  command: "start" | "continue" | "query" | "list";
  context: string;           // Feature description or current state
  sessionId?: string;        // For continue commands
}
```

**Commands:**

#### 1. `start` - Begin New Feature
Discovers operations, generates complete knowledge upfront, creates tasklist.

**Example**:
```json
{
  "command": "start",
  "context": "export vendor to Acumatica with duplicate check",
  "sessionId": null
}
```

**Returns**:
```typescript
{
  sessionId: string;          // "tdd_20250113_001"
  phase: "RED";               // Always starts in RED

  knowledge: {
    testCode: string;         // Complete test template
    implementationCode: string;  // Complete implementation template
    validationRules: string[];   // All validation rules
    errorMessages: string[];     // All error messages
    legacyFiles: FilePointer[];  // Pointers to legacy code
    requiredFields: Field[];     // Required fields with types
    optionalFields: Field[];     // Optional fields
    relatedOperations: string[]; // Related operations
  };

  tasklist: Task[];           // Complete numbered tasklist

  summary: {
    feature: string;
    operations: string[];
    estimated_complexity: "low" | "medium" | "high";
    total_tasks: number;
    requires_review: true;
    ready_for_execution: false;
  };
}
```

#### 2. `continue` - Progress Through Phases
Advances workflow through RED → GREEN → REFACTOR → COMPLETE with validation.

**Example**:
```json
{
  "command": "continue",
  "context": "tests are failing as expected",
  "sessionId": "tdd_20250113_001"
}
```

**Returns**:
```typescript
{
  sessionId: string;
  phase: "GREEN";             // Advanced to next phase
  guidance: string;           // What to do next
  allowedActions: string[];   // Valid next actions
  tasklist: Task[];           // Updated tasklist with progress
}
```

**Phase Enforcement:**
- RED phase: Must confirm tests fail
- GREEN phase: Must confirm tests pass
- REFACTOR phase: Optional improvements
- COMPLETE: Final verification

#### 3. `query` - Get Help
Context-aware help for errors, patterns, or questions.

**Example**:
```json
{
  "command": "query",
  "context": "getting 401 unauthorized error",
  "sessionId": null
}
```

**Returns**:
```typescript
{
  problem: string;            // Root cause
  solution: string;           // How to fix
  codeExample?: string;       // Code snippet if applicable
  relatedErrors?: string[];   // Similar issues
}
```

#### 4. `list` - Show All Operations
Lists all available operations by category.

**Example**:
```json
{
  "command": "list",
  "context": "",
  "sessionId": null
}
```

**Returns**:
```typescript
{
  totalCategories: number;
  totalOperations: number;
  categories: {
    name: string;
    description: string;
    operations: string[];
  }[];
}
```

## Session Management

**Features:**
- 30-minute timeout per session
- Maximum 100 concurrent sessions (LRU eviction)
- Thread-safe access with locking
- Automatic cleanup on every access
- Session tracks: phase, tasklist progress, feature context

**Session Lifecycle:**
```
1. start command → Creates session with 30min TTL
2. continue commands → Updates session.LastAccessedAt
3. 30min inactivity → Session auto-deleted
4. 100 sessions reached → LRU eviction
```

## Categories as Data

Categories organize 48 operations into logical groups:

| Category | Operations | Description |
|----------|------------|-------------|
| vendors | 4 | Vendor import/export/search |
| payments | 7 | Bill payments, voids, credits |
| purchaseOrders | 6 | PO operations and matching |
| accounts | 6 | GL/bank/payable accounts |
| fields | 10 | Custom fields, cost codes |
| items | 2 | Item search and categories |
| admin | 9 | Connect, config, validation |

Categories are **Knowledge files**, not tools:
- `Knowledge/categories.json` - Category index
- `Knowledge/operations/*.json` - Details per category

## Usage Examples

### Example 1: Vendor Export (Complete Flow)

```
User: "implement vendor export with duplicate check"

AI → kotlin_tdd_workflow(command="start", context="vendor export with duplicate check")

Response:
{
  "sessionId": "tdd_001",
  "phase": "RED",
  "knowledge": {
    "testCode": "// Complete test template...",
    "implementationCode": "// Complete implementation template...",
    "validationRules": ["vendorName required", "vendorId max 15 chars"],
    "legacyFiles": [
      "C#: integrations/Acumatica/Methods/ExportVendor.cs:25-150",
      "C#: integrations/Acumatica/Methods/GetMatchingVendorByStampliLink.cs:10-45"
    ]
  },
  "tasklist": [
    { "id": 1, "phase": "RED", "action": "Write failing test for exportVendor" },
    { "id": 2, "phase": "RED", "action": "Verify test fails" },
    { "id": 3, "phase": "GREEN", "action": "Implement exportVendor operation" },
    { "id": 4, "phase": "GREEN", "action": "Verify tests pass" },
    { "id": 5, "phase": "REFACTOR", "action": "Add duplicate check" }
  ]
}

AI writes tests using provided template...

AI → kotlin_tdd_workflow(command="continue", context="tests written and failing", sessionId="tdd_001")

Response:
{
  "sessionId": "tdd_001",
  "phase": "GREEN",
  "guidance": "Tests failing correctly. Now implement using the implementation template...",
  "tasklist": [
    { "id": 1, "status": "completed" },
    { "id": 2, "status": "completed" },
    { "id": 3, "status": "in_progress" }, // <-- Current
    ...
  ]
}

... continues through GREEN → REFACTOR → COMPLETE
```

### Example 2: Getting Help

```
AI → kotlin_tdd_workflow(command="query", context="401 unauthorized when calling Acumatica")

Response:
{
  "problem": "Authentication failure with Acumatica API",
  "solution": "Ensure AcumaticaAuthenticator.authenticate() is called before API calls. Check credentials in config.",
  "codeExample": "val auth = AcumaticaAuthenticator(config)\nauth.authenticate()\nval client = AcumaticaClient(auth)",
  "relatedErrors": ["403 Forbidden", "Invalid credentials"]
}
```

### Example 3: Listing Operations

```
AI → kotlin_tdd_workflow(command="list", context="")

Response:
{
  "totalCategories": 7,
  "totalOperations": 48,
  "categories": [
    {
      "name": "vendors",
      "description": "Vendor operations",
      "operations": ["exportVendor", "getVendors", "getMatchingVendorByStampliLink", "updateVendor"]
    },
    ...
  ]
}
```

## Benefits Summary

| Metric | Before (v2.0) | After (v3.0) | Improvement |
|--------|---------------|--------------|-------------|
| Tool Count | 16 | 2 | **-87.5%** |
| Entry Points | Multiple | 1 | **Single** |
| Token Usage | ~15k | ~1.5k | **-90%** |
| Prompts Needed | 6+ | 1 | **-83%** |
| Session Management | None | Full | **Added** |
| Phase Enforcement | Manual | Automatic | **Enforced** |

## Security Features

### 1. Input Validation
- 500 character limit on context
- Regex patterns to detect injection attempts
- Sanitization of all user inputs

### 2. Session Security
- Max 100 sessions (prevents DOS)
- 30-minute timeout (prevents resource leaks)
- Thread-safe access (prevents race conditions)

## Testing

### Integration Tests

**SingleEntryPointTests.cs:**
- Verifies only 2 tools exposed (kotlin_tdd_workflow + health_check)
- Validates complete knowledge returned upfront
- Tests session tracking through phases
- Tests TDD enforcement (can't skip RED phase)
- Tests query/help system
- Tests list command
- Tests graceful handling of unknown features

### Live LLM Tests

**FullWorkflowTests.cs:**
- Uses real Claude Code CLI
- Tests complete workflow from start to finish
- Logs all LLM↔MCP conversations
- Tracks metrics (tokens, cost, duration)
- Saves successful flows as golden patterns
- **Skipped by default** (run with `--filter "Category=LiveLLM"`)

See `TESTING_WITH_LIVE_LLM.md` for detailed instructions.

## Troubleshooting

### "Session not found"
- Session expired after 30 minutes
- Re-run `start` command to create new session

### "Invalid phase transition"
- Can't skip RED phase - tests must fail first
- Can't go back to previous phase
- Use `query` command for help

### "Unknown feature"
- Feature description doesn't match any category
- Use `list` command to see available operations
- Try more specific description

### "Context too long"
- Reduce context to <500 characters
- Focus on key requirements only

## Migration from v2.0

### Old Way (v2.0)
```
1. AI calls analyze_acumatica_feature
2. User reviews tasklist
3. User approves tasks
4. AI calls execute_acumatica_tasks
5. AI queries get_operation_details for help
6. AI queries get_errors for troubleshooting
```

### New Way (v3.0)
```
1. AI calls kotlin_tdd_workflow(command="start")
   → Gets complete knowledge + tasklist in one response
2. AI writes code using provided templates
3. AI calls kotlin_tdd_workflow(command="continue")
   → Session tracks progress automatically
4. AI uses kotlin_tdd_workflow(command="query") for help
   → No separate tools needed
```

### Deleted Tools
- ❌ `analyze_acumatica_feature` → Use `kotlin_tdd_workflow(command="start")`
- ❌ `execute_acumatica_tasks` → Execution is AI-driven, not tool-driven
- ❌ `get_operation_details` → Integrated into `start` command
- ❌ `get_errors` → Use `kotlin_tdd_workflow(command="query")`
- ❌ `search_operations` → Use `kotlin_tdd_workflow(command="list")`
- ❌ `recommend_operation` → Integrated into `start` discovery
- ❌ All other scattered tools

### Kept Tools
- ✅ `kotlin_tdd_workflow` - **SINGLE ENTRY POINT**
- ✅ `health_check` - Diagnostics only

## Best Practices

1. **Single Prompt**: User provides feature description once, AI manages rest
2. **Trust the Templates**: Use provided test/implementation templates as-is
3. **Follow TDD Phases**: Don't skip RED phase - tests must fail first
4. **Use File Pointers**: Legacy code locations provided, don't embed code
5. **Query for Help**: Use `query` command instead of guessing
6. **Session Timeout**: Complete workflow within 30 minutes or start new session

## Implementation Status

✅ **Completed (v3.0.0)**:
- Single entry point tool (`kotlin_tdd_workflow`)
- Complete knowledge upfront (no round trips)
- Session management (30min timeout, LRU eviction)
- TDD phase enforcement
- Query/help system integrated
- List operations command
- Thread-safe session access
- Integration tests (SingleEntryPointTests.cs)
- Live LLM test infrastructure (ClaudeCodeClient, ConversationLogger, SandboxManager)
- Metrics tracking (TestMetrics.cs)

## Future Enhancements

- [ ] Persistent session cache (survive server restart)
- [ ] Streaming responses for long tasklists
- [ ] Multi-feature workflows (batch operations)
- [ ] AI-powered task prioritization
- [ ] Automatic test execution validation
- [ ] Performance benchmarks vs v2.0

## Support

For issues or questions:
- Use `kotlin_tdd_workflow(command="query")` for context-aware help
- Check diagnostic: `health_check` tool
- Review logs in test output
- See `TESTING_WITH_LIVE_LLM.md` for testing guidance

---

**Version**: 3.0.0
**Last Updated**: January 13, 2025
**Architecture**: MCP Nuclear 2025 - Single Entry Point
