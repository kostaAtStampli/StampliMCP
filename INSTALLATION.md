# Acumatica MCP Server - Installation Guide

## For Other Developers on Your Team

### Quick Install (Recommended)

**Step 1: Clone the repo**
```bash
git clone https://github.com/kostaAtStampli/StampliMCP.git
cd StampliMCP
```

**Step 2: Build it**
```bash
dotnet build
```

**Step 3: Configure your IDE**

#### For Claude Desktop:
Edit `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\dev\\StampliMCP\\StampliMCP.McpServer.Acumatica"]
    }
  }
}
```
(Replace path with your clone location)

#### For Cursor:
Edit `.cursor/mcp.json` in your workspace:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\dev\\StampliMCP\\StampliMCP.McpServer.Acumatica"]
    }
  }
}
```

#### For Claude Code (VS Code):
Settings → Search "MCP" → Configure:
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\dev\\StampliMCP\\StampliMCP.McpServer.Acumatica"]
    }
  }
}
```

**Step 4: Restart IDE**

**Step 5: Test**
Ask Claude/Cursor: "Do you have access to Acumatica tools?"

Should show 9 tools available.

---

## What Gets Installed?

When you clone the repo, you get:

```
StampliMCP/
├── StampliMCP.McpServer.Acumatica/     ← The MCP server
│   ├── Knowledge/                       ← 13 JSON files with Acumatica intelligence
│   │   ├── operations/                  ← 51 operations across 10 categories
│   │   ├── enums.json                   ← 6 enum types
│   │   ├── error-catalog.json           ← All errors
│   │   └── test-config.json             ← Test patterns
│   ├── Tools/                           ← 9 MCP tools
│   ├── Services/                        ← Knowledge loading
│   └── Program.cs                       ← MCP server host
│
├── StampliMCP.McpServer.Acumatica.Tests/  ← 32 tests
├── README_IMPLEMENTATION_COMPLETE.md      ← Start here
├── QUICK_START.md                         ← 5-minute guide
└── FINAL_STATUS.md                        ← Complete details
```

---

## How It Works

**The MCP server runs locally on each dev's machine.**

When you ask Claude/Cursor a question about Acumatica:
1. IDE spawns the MCP server via `dotnet run`
2. MCP server loads knowledge from JSON files
3. IDE queries MCP tools via stdio
4. MCP returns targeted info (~2-4 KB)
5. IDE/LLM uses that info to help you

**No central server needed. No authentication. Just local stdio.**

---

## Advanced: Publish Once, Share Binary

If you don't want devs to clone source:

**You publish:**
```bash
cd StampliMCP.McpServer.Acumatica
dotnet publish -c Release -o \\shared-drive\tools\mcp-acumatica
```

**Devs configure:**
```json
{
  "mcpServers": {
    "acumatica": {
      "command": "\\\\shared-drive\\tools\\mcp-acumatica\\StampliMCP.McpServer.Acumatica.exe"
    }
  }
}
```

Everyone uses same binary, no build needed.

---

## Troubleshooting

### "MCP server not found"
- Check path in config is absolute and correct
- Test manually: `dotnet run --project <path>`

### "Tools don't appear"
- Reload IDE completely
- Check Knowledge/ files exist next to the .exe/.dll
- Run `dotnet build` if using source

### "Connection timeout"
- Test server starts: `cd StampliMCP.McpServer.Acumatica && dotnet run`
- Should start without errors, logs to console

---

## Updating

When Acumatica knowledge changes:

**Option 1: Git pull**
```bash
cd StampliMCP
git pull
```
Restart IDE, changes applied.

**Option 2: Edit JSON directly**
Edit files in `StampliMCP.McpServer.Acumatica/Knowledge/`
Restart IDE, changes applied (hot-reload).

---

## What Devs Get

Once installed, LLMs can:
- List all 51 Acumatica operations
- Get detailed info for any operation (flow traces, helpers, errors)
- Get test patterns and golden examples
- Get error catalogs with validation rules
- Search operations by keyword
- Get enum mappings
- Get base class inheritance info

**Use case:** "Write test for vendor export with duplicate handling"

LLM will:
1. Query MCP for exportVendor details
2. Get code pointers to AcumaticaDriver.java:412-470 (duplicate logic)
3. Get error messages (vendorName required, maxLength 60, etc.)
4. Get golden test example
5. Scan pointed files from C:\STAMPLI4\core\
6. Write complete test naturally

**No manual searching through code. No asking questions. Just works.**

---

## Repository

**GitHub:** https://github.com/kostaAtStampli/StampliMCP

**To contribute:**
```bash
git clone https://github.com/kostaAtStampli/StampliMCP.git
cd StampliMCP
# Make changes to Knowledge/*.json files
git add .
git commit -m "Add rich metadata for operation X"
git push
```

All devs `git pull` to get updates.

---

## Next Steps

1. Clone repo: `git clone https://github.com/kostaAtStampli/StampliMCP.git`
2. Build: `dotnet build`
3. Configure Claude/Cursor with path to project
4. Restart IDE
5. Test: "List Acumatica vendor operations"
6. Use: "Write test for vendor export"

**That's it!** 🚀

