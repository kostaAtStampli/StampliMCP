# Stampli MCP Server — Setup (Current)

This server is a single‑file Windows executable with embedded knowledge. Point your client at the exe; no extra services or auth.

## Publish the exe
```bash
"/mnt/c/Windows/System32/taskkill.exe" /F /IM stampli-mcp-acumatica.exe || true
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj \
  -c Release -r win-x64 --self-contained \
  /p:PublishSingleFile=true /p:PublishTrimmed=false /p:PublishAot=false --nologo
# Output: C:\Users\Kosta\source\repos\StampliMCP\StampliMCP.McpServer.Acumatica\bin\Release\net10.0\win-x64\publish\stampli-mcp-acumatica.exe
```

## Point your client to the exe

### Claude Desktop
Add to `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe"
    }
  }
}
```

### Cursor / VS Code MCP
Create `~/.cursor/mcp.json` (Cursor) or your editor’s MCP config:
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe"
    }
  }
}
```

### Codex CLI (TOML)
Append to `~/.codex/config.toml`:
```toml
[mcp_servers.stampli_acumatica]
command = "/mnt/c/Users/Kosta/source/repos/StampliMCP/StampliMCP.McpServer.Acumatica/bin/Release/net10.0/win-x64/publish/stampli-mcp-acumatica.exe"
cwd = "/mnt/c/Users/Kosta/source/repos/StampliMCP/StampliMCP.McpServer.Acumatica"
```

## Sanity checks
- `health_check` → text line includes `#STAMPLI-MCP-2025-10-GOLDEN#`
- `list_flows` → returns ~9 flows with descriptions and usedByOperations
- `get_flow_details("VENDOR_EXPORT_FLOW")` → constants/rules + marker link
- `query_acumatica_knowledge("", "flows")` → lists all flows (wildcard)

## Notes
- Exe size ≈ 108 MB (self‑contained .NET runtime + embedded resources)
- Always kill the running exe before publishing to avoid file lock
- Tools emit a short text summary for clients that hide structuredContent

