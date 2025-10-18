# Acumatica MCP Server Context

## Project Identity
MCP server for Acumatica ERP integration knowledge
Version: 4.0.0 (October 2025)
Protocol: MCP 2025-06-18
Build: BUILD_2025_10_18_PROMPT_FIX

## Architecture Overview
- **10 MCP tools** for querying 48 operations across 7 categories
- **Knowledge**: 48 embedded resources (JSON/MD/XML files)
- **Pattern**: Code GPS not document dumper - returns pointers to Java legacy code
- **Server**: Self-contained Windows exe (~31 MB) with embedded .NET runtime

## Key MCP Tools
1. `query_acumatica_knowledge` - Natural language search for operations
2. `health_check` - Server version and status
3. `kotlin_tdd_workflow` - TDD implementation workflow
4. `recommend_flow` - AI-powered flow recommendation
5. `get_flow_details` - Flow anatomy and constants
6. `validate_request` - Validate JSON payloads
7. `diagnose_error` - Root cause analysis
8. `get_kotlin_golden_reference` - Kotlin patterns (exportVendor only)
9. `check_knowledge_files` - List 48 embedded files
10. `challenge_scan_findings` - Verify scan accuracy

## Development Commands
```bash
# Build
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release --nologo

# Publish (use C:\ paths!)
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true \
  /p:PublishAot=false --nologo --no-build

# Kill before rebuild (ALWAYS!)
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe

# Reconnect MCP after rebuild
/mcp
```

## Critical Development Rules
- **ALWAYS kill process before rebuild** - exe gets locked
- **Use Windows paths in C#**: `@"C:\STAMPLI4\..."` not `/mnt/c/`
- **Rebuild after knowledge changes** - files are embedded resources
- **EnumName is nullable** in Operation model - not all ops have enums
- **FieldInfo objects required** - not plain strings in JSON
- **Test queries after reconnect** - MCP doesn't auto-reconnect

## Do NOT
- Edit legacy Java files directly
- Create documentation unless explicitly requested
- Add emojis unless user asks
- Assume tool counts from old docs (was wrong everywhere)
- Use `/home/kosta` for publish (creates `C:\home\kosta` instead!)
- Trust path conversions - be explicit with C:\ paths

## Recent Fixes
- Fixed vendor-operations.json and item-operations.json structure
- Made EnumName nullable in Operation model
- Added Titles to all MCP tools (2025 requirement)
- Consolidated 6 conflicting docs into 3 clear files

## Testing Examples
```csharp
// After rebuild and /mcp reconnect:
mcp__stampli-acumatica__query_acumatica_knowledge("vendor")
// Should return 6 vendor operations

mcp__stampli-acumatica__health_check()
// Should show version 4.0.0
```

## Links
- [User Documentation](./README.md) - Installation and usage
- [Technical Reference](./TECHNICAL.md) - WSL quirks and architecture
- [Knowledge Files](./StampliMCP.McpServer.Acumatica/Knowledge/) - Operation definitions
- [Java Legacy](C:\STAMPLI4\core\src\main\java\com\stampli\integration\acumatica) - Source code