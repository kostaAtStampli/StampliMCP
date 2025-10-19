# Acumatica MCP Server Context

**Version**: 4.0.0 | **Build**: GOLDEN (2025-10-19) | **Protocol**: MCP 2025-06-18

Key tools, 39 operations, 9 flows. Returns code pointers, not document dumps.
Self-contained Windows exe (≈108 MB) with embedded knowledge.

## Key MCP Tools
1. `list_flows` — Flow discovery (name, description, usedByOperations)
2. `get_flow_details` — Anatomy, constants, validation rules, code snippets
3. `query_acumatica_knowledge` — Search operations/flows/constants (elicitation-aware)
4. `list_operations` — Enumerate operations by category with flow mapping
5. `recommend_flow` — AI recommendation with alternatives
6. `validate_request` — Flow-driven preflight validation
7. `diagnose_error` — Category + related flow rules
8. `list_prompts` — Enumerate MCP prompts
9. `get_kotlin_golden_reference` — Kotlin reference (exportVendor)
10. `health_check` — Version/status + verification marker
11. `check_knowledge_files` — Embedded resource inventory

## Development Commands
```bash
# Build
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release --nologo

# Publish (use Windows paths; kill before publish)
"/mnt/c/Windows/System32/taskkill.exe" /F /IM stampli-mcp-acumatica.exe || true
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true \
  /p:PublishAot=false --nologo --no-build

# Kill before rebuild (ALWAYS!)
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe

# Reconnect MCP after rebuild
/mcp
```

## Critical Rules & Quirks

**MUST DO:**
- Kill process before rebuild (exe gets locked)
- Use `@"C:\STAMPLI4\..."` in C# code (NOT `/mnt/c/`)
- Rebuild after Knowledge/*.json changes (embedded)
- Run `/mcp` after rebuild (no auto-reconnect)
- Use FieldInfo objects in JSON (not strings)

**NEVER:**
- Edit Java files directly
- Create docs unless asked
- Add emojis unless requested
- Use `/home/kosta` for publish (creates `C:\home\kosta\`!)
- Return "Unknown" error category (use "GeneralError")

## Recent Fixes
- Fixed vendor-operations.json and item-operations.json structure
- Made EnumName nullable in Operation model
- Added Titles to all MCP tools (2025 requirement)
- Consolidated 6 conflicting docs into 3 clear files

## Testing Examples (A3 marker expected in text content)
```csharp
// After rebuild and /mcp reconnect:
mcp__stampli-acumatica__query_acumatica_knowledge("vendor")
// Should return 6 vendor operations

mcp__stampli-acumatica__health_check()
// status=ok version=4.0.0 ... #STAMPLI-MCP-2025-10-GOLDEN#

mcp__stampli-acumatica__list_flows()
mcp__stampli-acumatica__get_flow_details("VENDOR_EXPORT_FLOW")
```

## Links
- [User Documentation](./README.md) - Installation and usage
- [Technical Reference](./TECHNICAL.md) - WSL quirks and architecture
- [Knowledge Files](./StampliMCP.McpServer.Acumatica/Knowledge/) - Operation definitions
- [Java Legacy](C:\STAMPLI4\core\src\main\java\com\stampli\integration\acumatica) - Source code
