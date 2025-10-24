# Developer Quickstart – Unified MCP

## Prerequisites
- .NET 10 SDK (preview) installed (used throughout the repo).
- Windows + WSL (knowledge files reference paths in both).

## 1. Clone & Restore
```bash
git clone https://github.com/stampli/StampliMCP.git
cd StampliMCP
"/mnt/c/Program Files/dotnet/dotnet.exe" restore
```

## 2. Build Everything (Debug)
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build StampliMCP.slnx -c Debug --nologo
```

## 3. Run Unified MCP Locally
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj
```

Available modules today: `acumatica` (full) and `intacct` (stub).

## 4. Publish (Release, Self-Contained)
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj \
  -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true --nologo
```

Binary: `StampliMCP.McpServer.Unified/bin/Release/net10.0/win-x64/publish/stampli-mcp-unified.exe`

## 5. Configure Tooling
- `.claude/settings.local.json`, `mcp-runner.cmd`, `stampli-mcp-wrapper.sh`, etc. already point at the unified binary.
- For other clients, use the DLL path (`.../bin/Debug/net10.0/stampli-mcp-unified.dll`) or the published exe.

## 6. Adding an ERP Module
1. Create a new classlib under `StampliMCP.McpServer.<Erp>.Module`.
2. Populate `Knowledge/` (categories, operations, flows).
3. Implement knowledge/flow services and the module class.
4. Register the module in `StampliMCP.McpServer.Unified/Program.cs`.
5. Implement optional validation/diagnostic services by implementing the shared interfaces.
6. Run `erp__list_erps()` via any MCP client to verify it loads.

## 7. Tests
- E2E tests: `StampliMCP.E2E` exercises the unified server over stdio.
  - Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build StampliMCP.E2E -c Debug`
  - Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test StampliMCP.E2E -c Debug`
  - Optional for planner tests: set `CLAUDE_CLI_PATH` (e.g., `~/.local/bin/claude`) and `MCP_LOG_DIR`.
  - Planner apply-path tests can be gated behind an env flag and a local stub CLI.
