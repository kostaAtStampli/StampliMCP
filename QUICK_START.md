# Acumatica MCP Server - Quick Start Guide

## 5-Minute Setup

### Step 1: Build & Publish (2 min)
```bash
cd C:\Users\Kosta\source\repos\StampliMCP\StampliMCP.McpServer.Acumatica
dotnet publish -c Release -o publish
```

### Step 2: Configure Claude Desktop (1 min)
Open `%APPDATA%\Claude\claude_desktop_config.json` and add:
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

### Step 3: Restart Claude Desktop (30 sec)

### Step 4: Test (1 min)
Open Claude and ask:
```
Do you have access to Acumatica tools? List them.
```

Claude should show 9 tools available.

### Step 5: Real Usage
```
Write a test for Acumatica vendor export with duplicate handling
```

Claude will:
1. Call `get_operation("exportVendor")`
2. Get rich metadata with code pointers
3. Call `get_errors("exportVendor")`
4. Scan pointed files from C:\STAMPLI4\core\
5. Write complete test naturally

---

## Verify It Works

### Test 1: List Categories
```
Claude: "What Acumatica operation categories exist?"
```
Should return: vendors, items, purchaseOrders, payments, accounts, fields, admin, retrieval, utility, other

### Test 2: Get Operation Details
```
Claude: "Tell me about the exportVendor operation"
```

Should return:
- Summary
- Required fields (vendorName max 60, stampliurl required)
- Optional fields (vendorId, vendorClass, etc.)
- 7-layer flow trace
- 3 helper classes
- Golden test locations
- Error catalog reference

### Test 3: Write Test
```
Claude: "Write a test for vendor export that handles the duplicate vendor case"
```

Claude should:
- Query MCP for operation details
- Query MCP for errors
- Scan AcumaticaDriver.java lines 412-470 (duplicate logic)
- Scan golden test lines 140-165 (idempotency test)
- Write complete test with correct assertions

---

## Alternative: Cursor Setup

**Settings → Features → Model Context Protocol → Add Server:**
- Name: `Acumatica`
- Command: `C:\Users\Kosta\source\repos\StampliMCP\StampliMCP.McpServer.Acumatica\publish\StampliMCP.McpServer.Acumatica.exe`

Restart Cursor, tools available in chat.

---

## What You Get

**9 MCP Tools:**
1. list_categories
2. list_operations
3. get_operation (THE MAIN ONE)
4. get_operation_flow
5. search_operations
6. get_enums
7. get_test_config
8. get_errors
9. get_base_classes

**51 Operations:**
- 12 with full rich metadata (flow traces, helpers, errors, API details)
- 39 with code pointers + pattern references

**Complete Intelligence:**
- 6 enum type mappings
- Error catalog (validation, business, API)
- Test customer config with credentials pattern
- Golden test examples with specific methods
- Base class inheritance documentation

---

## Troubleshooting

**Tools don't appear:**
- Check config file syntax (valid JSON?)
- Check exe path is absolute and correct
- Restart Claude/Cursor completely

**MCP server crashes:**
- Test manually: `.\publish\StampliMCP.McpServer.Acumatica.exe`
- Check Knowledge/ folder exists in publish/
- Check logs in stderr

**Knowledge files not found:**
- Make sure you ran `dotnet publish`, not just `dotnet build`
- Verify `publish/Knowledge/` folder has all 13 JSON files

---

## Next Steps

1. ✅ Deploy to Claude Desktop
2. ✅ Test with: "Write test for vendor export"
3. ✅ Verify Claude uses MCP tools
4. ✅ Check generated test quality
5. ⭐ Give feedback for iteration

See `FINAL_STATUS.md` for complete implementation details.

