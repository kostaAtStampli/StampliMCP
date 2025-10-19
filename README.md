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

### Key Tools

| Tool | Purpose |
|------|---------|
| `list_flows` | List integration flows with descriptions and usedByOperations |
| `get_flow_details` | Flow anatomy, constants, rules, code snippets |
| `query_acumatica_knowledge` | Natural language search across operations/flows/constants |
| `list_operations` | Enumerate operations by category with flow mapping |
| `recommend_flow` | AI flow recommendation with alternatives/elicitation |
| `validate_request` | Pre‑flight JSON validation using flow rules |
| `diagnose_error` | Error diagnostics with related flow rules |
| `list_prompts` | List registered MCP prompts |
| `get_kotlin_golden_reference` | Kotlin golden reference (exportVendor) |
| `health_check` | Server status/version + verification marker |
| `check_knowledge_files` | List embedded knowledge resources |

## Build & Deploy (Windows exe)

```bash
# Kill running process before publish (prevents file lock)
"/mnt/c/Windows/System32/taskkill.exe" /F /IM stampli-mcp-acumatica.exe || true

# Build Release (optional)
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release --nologo

# Publish self-contained, single-file exe (≈108 MB)
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained \
  /p:PublishSingleFile=true /p:PublishTrimmed=false /p:PublishAot=false --nologo
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
// Health check (expects A3 marker in text content)
mcp__stampli-acumatica__health_check()
// status=ok version=4.0.0 ... #STAMPLI-MCP-2025-10-VERIFICATION-A3#

// List flows (expects 9 flows and marker)
mcp__stampli-acumatica__list_flows()

// Flow details (case-insensitive name)
mcp__stampli-acumatica__get_flow_details("VENDOR_EXPORT_FLOW")

// Knowledge query (empty flows scope lists all flows)
mcp__stampli-acumatica__query_acumatica_knowledge("", "flows")

// Validation (should flag VendorID > 15)
mcp__stampli-acumatica__validate_request("exportVendor", "{\"vendorName\":\"A\",\"VendorID\":\"1234567890123456789\"}")
```

## Support

- [Technical Reference](./TECHNICAL.md) - WSL quirks, architecture decisions
- [Java Legacy Code](file:///C:/STAMPLI4/core/src/main/java/com/stampli/integration/acumatica) - Source implementation
