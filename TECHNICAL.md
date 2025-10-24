# Technical Notes – Unified MCP

## Architecture

- **Unified host** (`StampliMCP.McpServer.Unified`) is the only executable.  It sets up logging, dependency injection, and the MCP transport.
- **ERP modules** (`StampliMCP.McpServer.<Erp>.Module`) are class libraries that own:
  - Embedded knowledge (`Knowledge/` JSON, Markdown, XML).
  - Services inheriting shared base classes (`KnowledgeServiceBase`, `FlowServiceBase`).
  - Optional validation/diagnostic/recommendation services implementing shared interfaces.
  - Module-specific tools/prompts (auto-registered by the unified host).
- **Shared library** (`StampliMCP.Shared`) exposes base services, models, and the `IErpModule` / `IErpFacade` abstractions used for dynamic dispatch.

Current module roster:

- Acumatica (full knowledge, flows, validation, developer tools)
- Intacct (stub module for wiring verification)

## Registry & Facades

`ErpRegistry` takes the registered modules, builds a case-insensitive alias map, and creates a new DI scope per tool invocation.  This prevents singleton/scoped conflicts and provides a strongly typed facade exposing module services.  Example:

```csharp
using var facade = registry.GetFacade("acumatica");
var operations = await facade.Knowledge.GetAllOperationsAsync(ct);
```

## Adding an ERP Module

1. Create the module project (`StampliMCP.McpServer.<Erp>.Module`).
2. Populate `Knowledge/categories.json`, `Knowledge/operations/<category>.json`, and `Knowledge/flows/*.json`.
3. Implement `<Erp>KnowledgeService`/`<Erp>FlowService` inheriting the shared bases and overriding `ResourcePrefix`.
4. Implement module class with aliases and capabilities:
   ```csharp
   public sealed class FooModule : IErpModule { ... }
   ```
5. Register module in `Program.cs` (add to `modules` array).
6. Provide optional services by implementing the shared interfaces:
   - `IErpValidationService`
   - `IErpDiagnosticService`
   - `IErpRecommendationService`
7. Run `dotnet build` + `erp__list_erps` to verify the module loads.

## Key Tool Patterns

- **Generic tools**: Live under `StampliMCP.McpServer.Unified/Tools/`.  They always accept an `erp` parameter and resolve the facade.
- **Module tools**: Remain in their respective module assemblies (e.g., Acumatica Kotlin TDD helpers).  The unified host registers module tool assemblies so clients can still call them.
- **Resource links**: Unified tools emit URIs of the form `mcp://stampli-unified/erp__<tool>?erp=<alias>&...` to encourage ERP-parameterized access.

## Build Targets

- `dotnet build` – Debug for unified server + modules.
- `dotnet publish -r win-x64 --self-contained true /p:PublishSingleFile=true` – produces `stampli-mcp-unified.exe`.
- Scripts (`mcp-runner.cmd`, wrappers) have been updated to point at the unified DLL/exe.

## Tests

`StampliMCP.E2E` drives the unified server over stdio for realistic, end‑to‑end validation. Use `dotnet test` on that project. Planner tests can use a real Claude CLI (set `CLAUDE_CLI_PATH`) or a local stub for deterministic runs.

## Aspire AppHost

`StampliMCP.AppHost` now references the unified server (`mcp-unified`) so local multi-service environments automatically use the new binary.

## Pending/Next

- Update documentation/templates when new ERP modules are added.
- Migrate any remaining code referencing the old Acumatica exe (search the repo for `McpServer.Acumatica`).
- Consider adding tooling for automatic module discovery instead of hard-coding the array in `Program.cs` once module count grows.
- Knowledge update planner: now exposed via `erp__knowledge_update_plan` with strict JSON parsing, environment-configurable CLI, and guarded apply to module `Knowledge/**`. Add E2E tests for dry-run/apply when feasible.
