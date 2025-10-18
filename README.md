# Acumatica MCP Server

MCP (Model Context Protocol) server for Acumatica ERP integration - provides AI with surgical access to 48 operations across 7 categories via code pointers instead of document dumps.

## Installation

### Claude Desktop

Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\path\\to\\stampli-mcp-acumatica.exe"
    }
  }
}
```

### VS Code / Cursor

Use the `.mcp.json` configuration in project root (check into git for team sharing).

## Usage

### Query Operations
```typescript
mcp__stampli-acumatica__query_acumatica_knowledge("vendor")
// Returns: 6 vendor operations with summaries

mcp__stampli-acumatica__query_acumatica_knowledge("payment")
// Returns: Operations related to payments
```

### MCP Tools (10)

| Tool | Purpose |
|------|---------|
| `query_acumatica_knowledge` | Search operations/flows |
| `kotlin_tdd_workflow` | TDD implementation workflow |
| `recommend_flow` | AI flow recommendation |
| `validate_request` | Pre-flight validation |
| `diagnose_error` | Error root cause analysis |
| `get_kotlin_golden_reference` | Kotlin patterns (exportVendor) |
| `get_flow_details` | Flow anatomy/constants |
| `health_check` | Server status |
| `check_knowledge_files` | List 48 embedded files |
| `challenge_scan_findings` | Verify scan accuracy |

## Build & Deploy

```bash
# Build Release
dotnet build -c Release --nologo

# Publish self-contained exe (~31 MB)
dotnet publish StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained \
  /p:PublishSingleFile=true /p:PublishAot=false
```

Output: `bin\Release\net10.0\win-x64\publish\stampli-mcp-acumatica.exe`

## Architecture

**"Code GPS, Not Document Dumper"**

Instead of dumping massive JSON:
1. LLM queries for lightweight operation metadata (~500 bytes)
2. MCP returns summary + code pointers (file:line_range)
3. LLM reads pointed files for deep understanding
4. Result: ~10KB context vs 50KB+ dump

## Project Structure

```
StampliMCP/
├── CLAUDE.md              # AI context (autoloads)
├── README.md              # This file
├── TECHNICAL.md           # Developer reference
└── StampliMCP.McpServer.Acumatica/
    ├── Tools/             # 10 MCP tool implementations
    ├── Services/          # Business logic
    ├── Models/            # Data structures
    └── Knowledge/         # 48 embedded JSON/MD files
        ├── vendor-operations.json
        ├── item-operations.json
        └── kotlin/        # TDD patterns
```

## Quick Test

After starting the server:
```javascript
// Test health
mcp__stampli-acumatica__health_check()
// Expected: { version: "4.0.0", status: "ok" }

// Test query
mcp__stampli-acumatica__query_acumatica_knowledge("vendor")
// Expected: 6 vendor operations
```

## Support

- [Technical Reference](./TECHNICAL.md) - WSL quirks, architecture decisions
- [Java Legacy Code](file:///C:/STAMPLI4/core/src/main/java/com/stampli/integration/acumatica) - Source implementation