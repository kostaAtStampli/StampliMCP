# Stampli MCP Server - Setup & Deployment Guide

## Overview

The Stampli Acumatica MCP (Model Context Protocol) server provides 14 AI-powered tools for Acumatica integration development. It includes Nuclear tools for autonomous Kotlin feature implementation with enforced TDD workflow.

## Quick Start

### Prerequisites
- .NET 10 SDK
- Windows 10/11 or WSL2

### Installation

#### For Claude Code (WSL/Linux)
```bash
# The executable is already built at:
/home/kosta/stampli-mcp-acumatica.exe

# Add to Claude Code:
claude mcp add stampli-acumatica /home/kosta/stampli-mcp-acumatica.exe

# Verify connection:
claude mcp list
# Should show: ✓ Connected
```

#### For Cursor
Create or edit `~/.cursor/mcp.json`:
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe",
      "args": []
    }
  }
}
```

#### For Claude Desktop
Edit `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe",
      "args": []
    }
  }
}
```

## Available Tools (14)

### Core Query Tools
1. **list_categories** - List all operation categories (vendors, items, POs, payments, etc.)
2. **search_operations** - Search operations by keyword
3. **get_operation_details** - Get surgical details for implementing specific operations
4. **get_enums** - Get enum mappings (VendorStatus, ItemType, etc.)
5. **get_base_classes** - Get base request/response class information
6. **get_test_config** - Get test customer configuration and golden test examples
7. **get_errors** - Get error catalog for operations

### Intelligence Tools
8. **recommend_operation** - Takes business requirement, recommends best Acumatica operation
9. **analyze_integration_complexity** - Analyzes feature complexity, effort, risks, dependencies
10. **troubleshoot_error** - Intelligent error troubleshooting with root cause analysis
11. **generate_test_scenarios** - Generates comprehensive test scenarios for operations

### Nuclear Tools (Autonomous Implementation)
12. **implement_kotlin_feature** - Autonomously implements Kotlin features with enforced 7-step TDD workflow
    - Discovery → Scan → Test(FAIL) → Implement → Test(PASS) → Completion
    - Single prompt, full autonomous execution

### Diagnostic Tools
13. **health_check** - Verify MCP server is running and responsive
14. **check_knowledge_files** - Check which Knowledge files are available

## Architecture

### Single-File Executable
- **Size**: 103MB self-contained executable
- **Dependencies**: None (all Knowledge files embedded as resources)
- **Runtime**: .NET 10
- **Knowledge Files**: 31 files embedded (JSON, MD, XML)

### Key Features
- ✅ No Native AOT (disabled for MCP JSON serialization compatibility)
- ✅ Clean stdio (all logs redirect to stderr per MCP specification)
- ✅ Embedded resources (no external file dependencies)
- ✅ Works from any directory
- ✅ Cross-platform (Windows, WSL, Linux)

## Building from Source

```bash
# Build
dotnet build StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release

# Publish single-file executable
dotnet publish StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release \
  -r win-x64 \
  --self-contained \
  /p:PublishSingleFile=true \
  /p:PublishAot=false

# Output location:
# StampliMCP.McpServer.Acumatica/bin/Release/net10.0/win-x64/publish/stampli-mcp-acumatica.exe
```

## Configuration

### Environment Variables
- `MCP_DEBUG=true` - Enable debug logging (logs to stderr)

### Logging
- All logs redirect to stderr (stdout reserved for JSON-RPC)
- Console output suppressed
- Clean JSON-RPC communication

## Troubleshooting

### "Failed to connect" in Claude Code on Windows
**Solution**: Use WSL-native path instead of Windows path
```bash
# Copy to WSL home
cp /mnt/c/path/to/stampli-mcp-acumatica.exe ~/

# Remove old config
claude mcp remove stampli-acumatica -s local

# Add with WSL path
claude mcp add stampli-acumatica /home/kosta/stampli-mcp-acumatica.exe
```

### Verify Server is Working
```bash
# Test manually
echo '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}},"id":1}' | ./stampli-mcp-acumatica.exe

# Should return JSON-RPC response with server info
```

### Check Knowledge Files
Use the `check_knowledge_files` tool to verify all embedded resources are accessible.

## Example Usage

### In Claude Code/Cursor/Claude Desktop

**Example 1: List Categories**
```
You: List all Acumatica operation categories

AI: Uses list_categories tool
Returns: vendors, items, purchaseOrders, payments, accounts, fields, admin, retrieval, utility, other
```

**Example 2: Search Operations**
```
You: Search for vendor-related operations

AI: Uses search_operations with query "vendor"
Returns: exportVendor, getVendors, getVendorCreditSearchList, getVendorPaymentData, getPoMatchingDataForVendors
```

**Example 3: Get Operation Details**
```
You: Get implementation details for the exportVendor operation

AI: Uses get_operation_details
Returns: Method signature, validation rules, error patterns, test config, file pointers to legacy code
```

**Example 4: Autonomous Feature Implementation**
```
You: Implement a Kotlin feature for vendor duplicate checking with name similarity matching

AI: Uses implement_kotlin_feature
Executes 7-step TDD workflow autonomously:
1. Discovery - finds relevant operations and patterns
2. Scan - analyzes legacy code
3. Test(FAIL) - writes failing test first
4. Implement - writes implementation
5. Test(PASS) - verifies test passes
6. Completion - provides summary
```

**Example 5: Business Requirement to Technical**
```
You: I need to pay multiple vendors at once from a single approval

AI: Uses recommend_operation
Returns: Recommended operation, alternatives, trade-offs, implementation approach, business considerations
```

## Technical Details

### Why Native AOT is Disabled
Native AOT was causing JSON serialization issues with MCP protocol. Research from Aug-Oct 2025 confirmed this is a known issue. Disabling AOT resolved all connection problems.

### Why Logs Go to Stderr
MCP specification requires stdout to be clean for JSON-RPC communication only. All logging output must go to stderr. This is configured via `LogToStandardErrorThreshold = LogLevel.Trace`.

### Why Embedded Resources
Embedding Knowledge files as resources ensures:
- No path resolution issues
- Single file deployment
- Works from any directory
- No external dependencies

## Knowledge Base

The server includes 31 embedded Knowledge files:
- **10 operation categories** (vendors, items, POs, payments, accounts, fields, admin, retrieval, utility, other)
- **6 enum mappings** (VendorStatus, ItemType, PurchaseOrderStatus, TransactionType, etc.)
- **Error catalogs** (authentication, operation-specific, API errors)
- **14 Kotlin migration guides** (patterns, workflows, architecture, TDD)
- **Test configurations** (golden test examples)
- **Base classes** (request/response inheritance)

## Version History

### v2.0.0 (Current)
- Added Interactive MCP Prompts (conversational AI guidance)
- Added 4 Intelligence Showcase Tools
- Nuclear MCP 2025: Autonomous Kotlin feature implementation

### v1.0.0
- Initial release with 14 tools
- Embedded Knowledge files
- Single-file deployment

## Support

For issues or questions:
1. Check `claude mcp list` - should show ✓ Connected
2. Use `health_check` tool to verify server status
3. Use `check_knowledge_files` tool to verify resources
4. Check logs with `MCP_DEBUG=true` environment variable

## License

Internal tool for Stampli development team.