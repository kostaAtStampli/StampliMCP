# Acumatica MCP Server - Deployment Guide

## How to Deploy & Use

The MCP server runs locally via **stdio** (not HTTP). LLM clients communicate with it via stdin/stdout.

## 🚀 Self-Contained Deployment (NEW)

### Build Options

#### Option A: Portable Build (Cross-Platform, ~31MB)
Works from any OS, includes .NET runtime:
```powershell
# Windows
./publish-portable.ps1 -Platforms "win-x64"

# macOS Apple Silicon
./publish-portable.ps1 -Platforms "osx-arm64"

# macOS Intel
./publish-portable.ps1 -Platforms "osx-x64"

# All platforms
./publish-portable.ps1
```

Output: `./publish/[platform]-portable/stampli-mcp-acumatica.exe`

#### Option B: Native AOT Build (Platform-Specific, ~15MB)
Fastest startup, smallest size, requires platform-specific build:
```powershell
# Windows (requires Visual Studio C++ tools)
./publish-windows.ps1

# macOS (must run on Mac)
./publish-mac.sh Release osx-arm64  # Apple Silicon
./publish-mac.sh Release osx-x64     # Intel
```

---

## Option 1: Claude Desktop (Recommended)

### Step 1: Publish the MCP Server
```bash
# Self-contained executable (NEW - Recommended)
./publish-portable.ps1 -Platforms "win-x64"
# Creates: ./publish/win-x64-portable/stampli-mcp-acumatica.exe

# Or traditional publish
cd StampliMCP.McpServer.Acumatica
dotnet publish -c Release -o publish
```

This creates: `stampli-mcp-acumatica.exe` (31MB self-contained)

### Step 2: Configure Claude Desktop
Open Claude Desktop config file:
- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
- **Mac:** `~/Library/Application Support/Claude/claude_desktop_config.json`

Add this configuration:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\publish\\StampliMCP.McpServer.Acumatica.exe",
      "args": []
    }
  }
}
```

Or use `dotnet run` (slower but no publish needed):
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica"]
    }
  }
}
```

### Step 3: Restart Claude Desktop

### Step 4: Verify
Open Claude Desktop chat and type:
```
Do you have access to Acumatica tools?
```

Claude should respond with available tools:
- `list_categories`
- `list_operations`
- `get_operation`
- `get_operation_flow`
- `search_operations`
- `get_enums`
- `get_test_config`
- `get_errors`

### Step 5: Test
```
Get details about the exportVendor operation
```

Claude will call `get_operation` tool and return rich metadata with code pointers.

---

## Option 2: Cursor (MCP Support)

### Step 1: Publish (same as Claude)

### Step 2: Configure Cursor
Open Cursor Settings:
1. Go to **Features** → **Model Context Protocol**
2. Click **Add Server**
3. Enter:
   - Name: `Acumatica`
   - Command: `C:\Users\Kosta\source\repos\StampliMCP\StampliMCP.McpServer.Acumatica\publish\StampliMCP.McpServer.Acumatica.exe`
   - Args: (leave empty)

Or add to `.cursor/mcp_config.json` in your workspace:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\publish\\StampliMCP.McpServer.Acumatica.exe",
      "args": []
    }
  }
}
```

### Step 3: Reload Cursor

### Step 4: Test in chat
```
List all Acumatica vendor operations
```

---

## Option 3: Cline/Codex CLI

Similar to Claude Desktop config. Find the tool's MCP configuration file and add:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "path/to/StampliMCP.McpServer.Acumatica.exe",
      "args": []
    }
  }
}
```

---

## Option 4: Via Aspire (Development Only)

### Step 1: Run AppHost
```bash
cd StampliMCP.AppHost
dotnet run
```

### Step 2: Access Aspire Dashboard
Open http://localhost:15000

You'll see:
- `apiservice` (HTTP)
- `webfrontend` (HTTP)
- `mcp-acumatica` (stdio - no HTTP endpoint)

### Note: 
MCP server in Aspire is for **development/debugging** only.  
For actual LLM use, deploy via Claude Desktop/Cursor (Option 1 or 2).

---

## Troubleshooting

### Issue: "MCP server not found"
**Solution:** Verify the exe path is correct in config. Use absolute paths.

### Issue: "Tools not appearing in Claude"
**Solution:** 
1. Check Claude Desktop config syntax (valid JSON?)
2. Restart Claude Desktop completely
3. Check Windows Event Viewer for .NET errors

### Issue: "Connection timeout"
**Solution:** 
1. Test MCP server manually:
   ```bash
   cd publish
   .\StampliMCP.McpServer.Acumatica.exe
   ```
   Should start without errors and log to stderr

2. Check Knowledge files copied to publish folder:
   ```bash
   ls publish\Knowledge
   ```
   Should have categories.json, operations/, enums.json, etc.

### Issue: "Knowledge files not found"
**Solution:** Ensure you ran `dotnet publish`, not just `dotnet build`.  
Publish copies Knowledge files to output.

---

## Testing the MCP Server

### Manual Test (Command Line)
```bash
cd StampliMCP.McpServer.Acumatica
dotnet run
```

Server starts and waits for stdio input (MCP protocol messages).  
Logs go to stderr, data to stdout.

**This is how LLMs communicate with it!**

### Automated Tests
```bash
cd StampliMCP.McpServer.Acumatica.Tests
dotnet test
```

Runs 32 tests verifying:
- ✅ 10 categories load
- ✅ 51 operations indexed
- ✅ Rich metadata for key operations
- ✅ Flow traces exist
- ✅ Error catalogs work
- ✅ All MCP tools function

---

## What LLMs See

When Claude/Cursor queries the MCP server:

**Available Tools (8):**
1. `list_categories` - Get all 10 operation categories
2. `list_operations` - List operations (all or by category)
3. `get_operation` - Get rich details for ONE operation
4. `get_operation_flow` - Get flow trace (service→driver→API)
5. `search_operations` - Search by keyword
6. `get_enums` - Get 6 enum type mappings
7. `get_test_config` - Get test customer + golden examples
8. `get_errors` - Get error catalog for operation

**Example LLM Query:**
```
User: "Write test for vendor export with duplicate handling"

Claude calls: list_operations(category="vendors")
→ Returns: 4 operations

Claude calls: get_operation(methodName="exportVendor")
→ Returns: 
  - Summary
  - Required fields (vendorName max 60, stampliurl required)
  - Optional fields (vendorId, vendorClass, etc.)
  - Flow trace (7 layers: Service→Router→Driver→Risk→Handler→Mapper→API)
  - Code pointers to scan:
    * AcumaticaDriver.java:386-398 (main method)
    * AcumaticaDriver.java:412-470 (duplicate check logic)
    * CreateVendorHandler.java:22-90 (validation)
  - Golden test: AcumaticaDriverCreateVendorITest.java:140-165 (idempotency test)
  - Error catalog reference

Claude calls: get_errors(operation="exportVendor")
→ Returns:
  - Validation errors (vendorName required, maxLength 60, etc.)
  - Business errors (link mismatch message)
  - API errors (400, 401, 500)

Claude scans: C:\STAMPLI4\core\finsys-drivers\acumatica\...\AcumaticaDriver.java lines 412-470

Claude scans: Golden test lines 140-165

Claude writes: Complete test with duplicate handling, validation, correct assertions
```

---

## File Locations After Publishing

```
publish/
├── StampliMCP.McpServer.Acumatica.exe
├── StampliMCP.McpServer.Acumatica.dll
├── ModelContextProtocol.dll
├── (other dependencies)
└── Knowledge/
    ├── categories.json
    ├── operations/
    │   ├── vendors.json
    │   ├── items.json
    │   ├── (8 more category files)
    ├── enums.json
    ├── test-config.json
    └── error-catalog.json
```

Point Claude/Cursor to the `.exe` file. Knowledge files must be in `Knowledge/` subfolder.

---

## Next Steps

1. **Deploy to Claude Desktop** (Option 1)
2. **Test with real task:** "Write test for vendor export"
3. **Verify LLM uses MCP tools** (check Claude logs)
4. **Iterate on knowledge files** if LLM needs more detail

---

## Future ERPs

To add NetSuite/QuickBooks/etc:
1. Copy `StampliMCP.McpServer.Acumatica` → `StampliMCP.McpServer.NetSuite`
2. Replace Knowledge files with NetSuite operations
3. Update AppHost.cs to add new MCP server
4. Deploy to Claude with different name:
   ```json
   {
     "mcpServers": {
       "acumatica": { ... },
       "netsuite": { "command": "path/to/NetSuite.exe" }
     }
   }
   ```

Each ERP gets its own MCP server, own knowledge base, own deployment.

