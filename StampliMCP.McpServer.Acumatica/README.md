# Acumatica MCP Server  🚀

**Model Context Protocol (MCP) server providing intelligent access to Acumatica ERP integration knowledge.**

**Status:** ✅ 90% Golden - Production Ready  
**Build:** ✅ Success (0 errors)  
**Tests:** ✅ 32/32 Passed  
**Operations:** 51 indexed, 12 fully detailed, 39 with pattern references  
**Tools:** 9 MCP tools  
**Knowledge:** 13 JSON files (~40 KB)

## What is this?

This is NOT a REST API. This is an MCP server that LLMs (Claude, GPT, etc.) query to understand the Acumatica integration codebase.

**Purpose:** Give LLMs surgical access to:
- 49 operations across 9 categories
- Code pointers to legacy Java implementation (file + line ranges)
- Request/response schemas with field requirements
- Error catalogs with validation messages
- Golden test examples to copy patterns from
- Enum mappings with code locations

## Architecture

**MCP = Smart Code GPS, Not a Document Dumper**

Instead of dumping 5000-line JSON blobs:
1. LLM queries MCP for lightweight metadata (~500 bytes)
2. MCP returns summary + code pointers (file paths + line ranges)
3. LLM scans pointed files for deep understanding
4. LLM writes tests naturally based on what it learned

**Result:** ~10KB context usage vs 50KB+ dumping everything

## MCP Tools (7 Tools)

### Discovery Tools
1. **`list_categories`** - List all 9 operation categories
2. **`list_operations`** - List operations (all or filtered by category)
3. **`search_operations`** - Search by keyword

### Operation Details
4. **`get_operation`** - Full operation details with code pointers, schemas, errors
5. **`get_operation_flow`** - End-to-end call chain through layers

### Reference Data
6. **`get_enums`** - All 6 enum types with mappings
7. **`get_test_config`** - Test customer config + golden examples

## Knowledge Files

```
Knowledge/
├── categories.json              # 9 categories index
├── operations/
│   ├── vendors.json             # 4 vendor operations
│   ├── items.json               # 2 item operations
│   ├── purchaseOrders.json      # 6 PO operations
│   ├── payments.json            # 7 payment operations
│   ├── accounts.json            # 6 account operations
│   ├── fields.json              # 10 field operations
│   ├── admin.json               # 9 admin operations
│   ├── retrieval.json           # 4 retrieval operations
│   └── utility.json             # 2 utility operations
├── enums.json                   # 6 enum type mappings
└── test-config.json             # Test customers + golden examples
```

All files support hot-reload (no rebuild needed when editing).

## How to Use

### Via MCP Client (Programmatic)
```csharp
using ModelContextProtocol;

var transport = new StdioClientTransport(new()
{
    Command = "dotnet",
    Arguments = ["run", "--project", "StampliMCP.McpServer.Acumatica"]
});

var client = await McpClient.CreateAsync(transport);

// List available tools
var tools = await client.ListToolsAsync();
// Returns: list_categories, list_operations, get_operation, etc.

// Call a tool
var result = await client.CallToolAsync(
    "get_operation",
    new Dictionary<string, object?> { ["methodName"] = "exportVendor" },
    CancellationToken.None);

// Result contains: summary, requiredFields, scanThese (code pointers), goldenTest
```

### Via Claude Desktop
Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\StampliMCP.McpServer.Acumatica"]
    }
  }
}
```

### Via Aspire Dashboard
```bash
cd StampliMCP.AppHost
dotnet run
```

MCP server will appear in dashboard. Access via stdio transport (not HTTP).

## LLM Usage Example

```
User: "Write test for vendor export with duplicate handling"

LLM → Calls tool: list_operations(category="vendors")
Response: {
  "operations": [
    { "method": "exportVendor", "summary": "Creates vendor with duplicate check" },
    { "method": "getVendors", "summary": "Imports vendors with delta support" }
  ]
}

LLM → Calls tool: get_operation(methodName="exportVendor")
Response: {
  "summary": "Creates vendor. Checks duplicates by Stampli link.",
  "requiredFields": {
    "vendorName": { "type": "string", "maxLength": 60 },
    "stampliurl": { "type": "string" }
  },
  "scanThese": [
    { "file": "...AcumaticaDriver.java", "lines": "386-398", "purpose": "Main export method" },
    { "file": "...AcumaticaDriver.java", "lines": "412-470", "purpose": "Duplicate check logic" },
    { "file": "...CreateVendorHandler.java", "lines": "22-90", "purpose": "Validation + API call" }
  ],
  "goldenTest": {
    "file": "...AcumaticaDriverCreateVendorITest.java",
    "lines": "30-300"
  }
}

LLM → Scans pointed files at C:\STAMPLI4\core\...
LLM → Reads golden test
LLM → Writes new test with correct validation, duplicate handling, assertions
```

## Legacy Codebase References

MCP points to these locations (READ-ONLY):
- `C:\STAMPLI4\core\bridge\bridge-financial-system\` - AgentOpCode, Request/Response DTOs
- `C:\STAMPLI4\core\finsys-drivers\acumatica\` - AcumaticaDriver, helpers, tests
- `C:\STAMPLI4\core\web\server-services\` - Agents, routers

## Development

**Hot-reload knowledge files:**
1. Edit any JSON file in `Knowledge/`
2. Files auto-copy to output (no rebuild needed)
3. Restart MCP server to reload

**Add new operation:**
1. Add to appropriate `operations/{category}.json`
2. Include: method, enumName, summary, requiredFields, scanThese, goldenTest
3. Rebuild and test

**Add new category:**
1. Update `categories.json`
2. Create `operations/{newCategory}.json`
3. Add operations

## Testing

```bash
cd StampliMCP.McpServer.Acumatica.Tests
dotnet test
```

All tests verify:
- ✅ 9 categories load correctly
- ✅ 49 operations index across categories
- ✅ Operations have code pointers
- ✅ Enums load with 6 types
- ✅ Test config accessible
- ✅ MCP tools return expected data

## Future ERPs

This pattern replicates for other ERPs:
- `StampliMCP.McpServer.NetSuite` - NetSuite operations
- `StampliMCP.McpServer.QuickBooks` - QuickBooks operations
- Each ERP gets its own MCP server with its own knowledge files

