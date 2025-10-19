# Acumatica MCP Server - Quick Start Guide

## 5‑Minute Setup (Current)

### Step 1: Publish the exe
```bash
"/mnt/c/Windows/System32/taskkill.exe" /F /IM stampli-mcp-acumatica.exe || true
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained \
  /p:PublishSingleFile=true /p:PublishTrimmed=false /p:PublishAot=false --nologo
```

### Step 2: Point your client to the exe
Add the exe path to your MCP client (Claude Desktop, Cursor, Codex CLI). See `MCP_SERVER_SETUP.md`.

### Step 3: Test
```
mcp__stampli-acumatica__health_check()
mcp__stampli-acumatica__list_flows()
mcp__stampli-acumatica__get_flow_details("VENDOR_EXPORT_FLOW")
mcp__stampli-acumatica__query_acumatica_knowledge("", "flows")
```

---

## What You Get (Highlights)
- list_flows / get_flow_details → flow‑first discovery
- query_acumatica_knowledge → natural‑language search (wildcard for flows)
- validate_request / diagnose_error → flow‑aware guardrails

---

## Alternative: Cursor/VS Code
Add the exe path in your editor’s MCP configuration. Restart the editor, then call `health_check`.

---

## What You Get

**Key tools:** list_flows, get_flow_details, query_acumatica_knowledge, list_operations, recommend_flow, validate_request, diagnose_error, list_prompts, get_kotlin_golden_reference, health_check, check_knowledge_files

---

## Troubleshooting

**Tools don’t appear:**
- Check config file syntax (valid JSON?)
- Check exe path is absolute and correct
- Restart Claude/Cursor completely

**MCP server crashes:**
- Test manually: `.\publish\StampliMCP.McpServer.Acumatica.exe`
- Check Knowledge/ folder exists in publish/
- Check logs in stderr

**Knowledge files not found:**
- Knowledge is embedded; republish if you changed JSON

---

## Next Steps

1. ✅ Deploy to Claude Desktop
2. ✅ Test with: "Write test for vendor export"
3. ✅ Verify Claude uses MCP tools
4. ✅ Check generated test quality
5. ⭐ Give feedback for iteration

See `README.md` for current tool list and examples.

