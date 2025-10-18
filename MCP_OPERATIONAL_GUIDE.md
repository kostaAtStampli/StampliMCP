# MCP Operational Cheatsheet

## Build & Publish
```bash
# Build Release
"/mnt/c/Program Files/dotnet/dotnet.exe" build \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release --nologo

# Publish (CRITICAL: Use C:\home\kosta not /home/kosta/)
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:PublishAot=false \
  -o "C:\home\kosta" --nologo --no-build

# Copy to WSL home for testing
cp /mnt/c/home/kosta/stampli-mcp-acumatica.exe /home/kosta/
```

## Kill Cached Processes (ALWAYS DO THIS!)
```bash
# Kill WSL processes
pkill -f stampli-mcp-acumatica

# Kill Windows processes
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe 2>&1 || true
```

## Path Handling (CRITICAL!)

### Publish Location Trap
```
-o /home/kosta/  → Creates C:\home\kosta\ (Windows path, NOT WSL!)
-o C:\home\kosta → Creates C:\home\kosta\ (correct, explicit)
```
**Solution:** Publish to `C:\home\kosta` then copy to `/home/kosta/` for testing.

### Windows .exe Path Interpretation
```
MCP server runs as Windows .exe → Use C:\ paths NOT /mnt/c/ paths
/mnt/c/STAMPLI4 → Interpreted as C:\mnt\c\STAMPLI4 (WRONG!)
C:\STAMPLI4     → Correct
```
**In C# file reads:** Use `@"C:\STAMPLI4\..."` format
**In JSON files:** Use `C:\\\\STAMPLI4\\\\...` (double backslash escape)

### WSL Path Conversions
```
Windows: C:\Users\Kosta\...       → WSL: /mnt/c/Users/Kosta/...
Windows: C:\home\kosta\...         → WSL: /mnt/c/home/kosta/...
Windows: C:\Program Files\...     → WSL: "/mnt/c/Program Files/..." (QUOTE IT!)
WSL: /home/kosta/...               → Linux home (NOT visible to Windows .exe!)
```

## MCP Logs (Source of Truth)
**PRIMARY:** Test-isolated (set by MCP_LOG_DIR env var)
```
/mnt/c/Users/Kosta/AppData/Local/Temp/mcp_test_*_YYYYMMDD_HHMMSS/mcp_flow_*.jsonl
```

**SECONDARY:** Fixed location (always written, fallback works)
```
/mnt/c/Users/Kosta/AppData/Local/Temp/mcp_logs/mcp_responses_YYYYMMDD.jsonl
```

**View latest:**
```bash
tail -1 /mnt/c/Users/Kosta/AppData/Local/Temp/mcp_logs/mcp_responses_*.jsonl
```

## Test Commands
```bash
# Build tests
"/mnt/c/Program Files/dotnet/dotnet.exe" build \
  StampliMCP.McpServer.Acumatica.Tests/StampliMCP.McpServer.Acumatica.Tests.csproj -c Debug --nologo

# Run specific test
"/mnt/c/Program Files/dotnet/dotnet.exe" test \
  --filter "FullyQualifiedName~ClaudeCli_Should_Call_KotlinTddWorkflow" \
  -c Debug --no-build

# Run all minimal tests
"/mnt/c/Program Files/dotnet/dotnet.exe" test \
  --filter "FullyQualifiedName~MinimalTests" -c Debug --no-build
```

## MCP Log Format
```json
{
  "timestamp": "2025-10-14T15:42:47.112Z",
  "tool": "kotlin_tdd_workflow",
  "flowName": "standard_import_flow",
  "responseSize": 39119,
  "operationCount": 13
}
```

## Verify MCP Capabilities

### Check Tools & Prompts Registered
```bash
# After MCP reconnect, verify in Claude CLI:
# Should see:
# Capabilities: tools, prompts

# Or via health_check tool:
claude --print "Call health_check tool from stampli-acumatica"
# Expected: version: "4.0.0-BUILD_2025_10_18_PROMPT_FIX"
```

### Expected Counts
- ✅ **9 Tools**: query_acumatica_knowledge, recommend_flow, get_flow_details, validate_request, diagnose_error, get_kotlin_golden_reference, kotlin_tdd_workflow, health_check, check_knowledge_files
- ✅ **5 Prompts**: kotlin_tdd_tasklist, implement_feature_guided, plan_comprehensive_tests, debug_with_expert, analyze_integration_strategy

**SDK 0.4.0-preview.2 Workaround:**
- Program.cs uses explicit `.WithPrompts<T>()` per prompt (not `.WithPromptsFromAssembly()`)
- All prompt classes are `sealed` (not `static`) to work with generic registration

## Debugging & Verification

### Check Log Timestamps (CRITICAL!)
```bash
# Check latest log timestamp
tail -1 /mnt/c/Users/Kosta/AppData/Local/Temp/mcp_logs/mcp_responses_*.jsonl | grep timestamp

# Verify timestamp matches current test run time
# Old timestamps = testing old executable!
```

### Response Size Smoke Test
```
Known good response sizes:
- 39,811 chars = No Kotlin golden reference (BEFORE fix)
- 42,763 chars = Error response (DirectoryNotFoundException)
- 72,048 chars = With Kotlin golden reference (CORRECT)

+32KB difference = Kotlin files successfully embedded
```

### Verify Executable Location
```bash
# Check WSL home (test location)
ls -lh /home/kosta/stampli-mcp-acumatica.exe

# Check Windows publish location
ls -lh /mnt/c/home/kosta/stampli-mcp-acumatica.exe
```

## Full Rebuild Cycle
```bash
cd /mnt/c/Users/Kosta/source/repos/StampliMCP

# 1. Build
"/mnt/c/Program Files/dotnet/dotnet.exe" build \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release --nologo

# 2. Kill processes (WSL + Windows)
pkill -f stampli-mcp-acumatica
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe 2>&1 || true

# 3. Publish (to C:\home\kosta not /home/kosta/)
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:PublishAot=false \
  -o "C:\home\kosta" --nologo --no-build

# 4. Copy to WSL home for testing
cp /mnt/c/home/kosta/stampli-mcp-acumatica.exe /home/kosta/

# 5. Verify
ls -lh /home/kosta/stampli-mcp-acumatica.exe
```

## MCP Primitives (Tools vs Prompts)

**Tools** - Return JSON data (e.g., `kotlin_tdd_workflow`)
- LLM calls tool → Gets data → Decides what to do with it
- Can't enforce format (data is buried in JSON)

**Prompts** - Return conversation starters (e.g., `kotlin_tdd_tasklist`)
- LLM invokes prompt → Gets pre-written conversation → Continues that conversation
- Enforces format via conversation context (primes LLM identity)

**Format Enforcement Flow:**
1. Test invokes `kotlin_tdd_tasklist` PROMPT
2. Prompt establishes format in system message + demonstrates in assistant message
3. LLM calls `kotlin_tdd_workflow` TOOL (within prompt context)
4. LLM outputs in ═══ FILES SCANNED ═══ format (conversation enforces)

**Result:** 100% format compliance (was 0% with tool-only approach)

## Current Status & Lessons Learned
✅ Format enforcement via MCP Prompt
✅ Test-isolated logging (fallback to FIXED location works)
✅ Process management (kill WSL + Windows before rebuild)
✅ Ground truth validation via MCP logs
✅ Path handling fixed (C:\ for Windows .exe, not /mnt/c/)
✅ Publish location corrected (`C:\home\kosta` → copy to `/home/kosta/`)
✅ Response size metrics for smoke testing (39KB → 72KB = success)
✅ Log timestamp verification (old timestamps = testing old exe)

## Common Gotchas
❌ **DON'T** publish to `-o /home/kosta/` → creates `C:\home\kosta\`
❌ **DON'T** use `/mnt/c/` paths in C# file reads → use `C:\`
❌ **DON'T** forget to kill both WSL and Windows processes
❌ **DON'T** trust response size alone → verify log timestamps
✅ **DO** publish to `C:\home\kosta` then copy to `/home/kosta/`
✅ **DO** check log timestamps match test run time
✅ **DO** use response size as smoke test (72KB expected)
