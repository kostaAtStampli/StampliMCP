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

## Known Issues
**Test-isolated logging fails:** MCP_LOG_DIR env var doesn't pass to WSL exe → FIXED location works (fallback implemented)
**Stale responses:** Kill process didn't work → Check `ps aux | grep stampli` for survivors
**Tests fail on format:** LLM is unpredictable → Rely on MCP logs, not test assertions
