# MCP Operational Cheatsheet

## Build & Publish
```bash
# Build Release
"/mnt/c/Program Files/dotnet/dotnet.exe" build \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release --nologo

# Publish
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:PublishAot=false \
  -o /home/kosta/ --nologo --no-build
```

## Kill Cached Process (ALWAYS DO THIS!)
```bash
ps aux | grep stampli-mcp-acumatica | grep -v grep | awk '{print $2}' | xargs -r kill -9
```

## WSL Path Conversions
```
Windows: C:\Users\Kosta\...        → WSL: /mnt/c/Users/Kosta/...
Windows: C:\home\kosta\...          → WSL: /home/kosta/... (Linux home)
Windows: C:\Program Files\...      → WSL: "/mnt/c/Program Files/..." (QUOTE IT!)
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

## Full Rebuild Cycle
```bash
cd /mnt/c/Users/Kosta/source/repos/StampliMCP
"/mnt/c/Program Files/dotnet/dotnet.exe" build StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release --nologo
ps aux | grep stampli-mcp-acumatica | grep -v grep | awk '{print $2}' | xargs -r kill -9
"/mnt/c/Program Files/dotnet/dotnet.exe" publish StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:PublishAot=false -o /home/kosta/ --nologo --no-build
md5sum /home/kosta/stampli-mcp-acumatica.exe
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

## Current Status
✅ Format enforcement via MCP Prompt
✅ Test-isolated logging (fallback to FIXED location works)
✅ Process management (kill before rebuild)
✅ Ground truth validation via MCP logs
