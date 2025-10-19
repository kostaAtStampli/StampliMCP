# Technical Reference

## Critical WSL/Windows Path Quirks

```bash
# Publishing: NEVER use WSL paths for output!
-o /home/kosta/              # ❌ Creates C:\home\kosta\
-o "C:\home\kosta"           # ✅ Correct Windows path

# In C# code: MCP runs as Windows .exe
@"C:\STAMPLI4\..."           # ✅ Native Windows path
"/mnt/c/STAMPLI4/..."        # ❌ Becomes C:\mnt\c\STAMPLI4

# WSL commands: Quote paths with spaces
"/mnt/c/Program Files/dotnet/dotnet.exe"  # ✅ Quoted
```

## Process Management

### Always Kill Before Rebuild
```bash
# Windows process (from WSL)
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe

# Check if running
/mnt/c/Windows/System32/tasklist.exe | grep stampli-mcp
```

### MCP Reconnection
After killing process:
1. Rebuild/publish
2. Run `/mcp` in Claude to reconnect
3. Test with `health_check()`

## Architecture Decisions

### Why Embedded Resources?
- Single-file deployment (≈108 MB)
- No file path issues at runtime
- Knowledge travels with exe
- Trade-off: Must rebuild for knowledge changes

### Model Design Choices
```csharp
// EnumName is nullable - not all operations have enums
public string? EnumName { get; init; }

// RequiredFields uses FieldInfo objects, not strings
public Dictionary<string, FieldInfo> RequiredFields { get; init; }
```

### JSON Structure Requirements
```json
{
  "requiredFields": {
    "vendorName": {
      "type": "string",
      "maxLength": 60,
      "description": "Vendor name"
    }
  }
}
// NOT: "vendorName": "max 60 chars"
```

## Knowledge Structure

### Embedded Files (48 total)
```
Knowledge/
├── categories.json              # 7 operation categories
├── vendor-operations.json       # 6 vendor ops (NEW)
├── item-operations.json         # 4 item ops (NEW)
├── operations.{category}.json   # Legacy format (still used)
├── flows.*.json                 # 9 integration patterns
└── kotlin/
    ├── GOLDEN_PATTERNS.md       # exportVendor implementation
    ├── TDD_WORKFLOW.md          # Test-driven methodology
    └── *.xml                    # Workflow definitions
```

### Operation Model
```csharp
public sealed record Operation
{
    required public string Method { get; init; }
    public string? EnumName { get; init; }  // Nullable!
    required public string Summary { get; init; }
    required public string Category { get; init; }
    public Dictionary<string, FieldInfo> RequiredFields { get; init; }
    // ... other fields
}
```

## Common Issues & Solutions

| Issue | Cause | Fix |
|-------|-------|-----|
| Empty query results | JSON structure mismatch | RequiredFields must be FieldInfo objects, not strings |
| MCP not reconnecting | Claude doesn't auto-reconnect | Run `/mcp` command manually |
| Build access denied | exe still running | `taskkill /F /IM stampli-mcp-acumatica.exe` |
| Knowledge not updating | Embedded resources | Rebuild after changing Knowledge/*.json |
| Wrong error category | Pattern missing | Check ErrorDiagnosticTool.CategorizeError() |

## Testing Checklist

After changes:
1. Kill existing process
2. Build Release
3. Publish exe
4. Run `/mcp` to reconnect
5. Test `health_check()` - verify version
6. Test `query_acumatica_knowledge("vendor")` - should return 6 ops
7. Test `check_knowledge_files()` - should show 48 files

## Performance Notes

- Query returns ~500 bytes metadata
- Full operation details ~2–5 KB
- Embedded resources load once, cached in memory
- Single-file exe ≈108 MB (includes .NET runtime)

## Debug Commands

```bash
# List embedded resources
dotnet run -- list-resources

# Check what's actually embedded
grep "EmbeddedResource" *.csproj

# Verify JSON structure
jq . Knowledge/vendor-operations.json
```

## Version History

- **4.0.0** (Oct 2025) - Current, 10 tools, fixed JSON structure
- **3.0.0** - Obsolete "Nuclear" single-tool architecture
- **2.x** - Legacy multi-tool versions
- **1.x** - Initial prototypes
