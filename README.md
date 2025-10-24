# Stampli MCP – Unified Server

One executable MCP server that exposes ERP‑specific knowledge and tools behind a unified API. Tools return structured results and can elicit missing inputs on supported clients.

## Quick Start
- Build/publish (self‑contained Windows):
```
dotnet publish StampliMCP.McpServer.Unified -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
- Point your MCP client at the published `stampli-mcp-unified.exe`.
- Try `erp__health_check()`, `erp__query_knowledge(erp="acumatica", query="vendor", scope="operations")`, `erp__recommend_flow(erp="acumatica", useCase="export vendors")`.

## Documentation (Canonical)
- docs/LLM_START_HERE.md — orientation for humans and LLMs
- docs/LLM_ARCHITECTURE.md — unified host + modules (with code refs)
- docs/LLM_TOOLS_AND_SCHEMAS.md — tools catalog + structured result schemas
- docs/LLM_KNOWLEDGE_AND_FLOWS.md — knowledge/flows JSON shapes and usage
- docs/DEVELOPER_GUIDE.md — build/publish/extend/test
- docs/MANAGER_BRIEF.md — value, KPIs, roadmap

## Notes
- Elicitation is typed and optional; tools fall back gracefully if unsupported.
- Knowledge and flows are embedded per ERP module and loaded at runtime.
