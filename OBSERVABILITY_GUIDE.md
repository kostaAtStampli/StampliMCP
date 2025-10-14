# MCP Observability Guide

## What We Added (4 hours Phase 1)

### System.Diagnostics.Metrics (.NET Built-in)
- **File:** `Services/MetricsService.cs`
- **Metrics:**
  - `mcp.tool.calls` - Counter (by tool, command, success)
  - `mcp.tool.duration` - Histogram in ms (p50/p95/p99)
  - `mcp.tool.tokens` - Counter (response size)
  - `mcp.tool.errors` - Counter (failures)

### Serilog Structured Logging
- **Format:** Compact JSON (`CompactJsonFormatter`)
- **Output:**
  - Console (stderr - MCP protocol)
  - `/tmp/mcp_logs/structured.jsonl` (daily rolling, 30-day retention)

### Integration
- `kotlin_tdd_workflow` tool fully instrumented
- Metrics + logging on every execution
- Flow name tracked in metrics

---

## How to Use

### View Live Metrics
```bash
# Install dotnet-counters (one time)
dotnet tool install --global dotnet-counters

# Monitor live
dotnet-counters monitor --process-id $(pgrep -f stampli-mcp-acumatica) StampliMCP.Acumatica

# Output:
[mcp.tool.calls]
    tool=kotlin_tdd_workflow,command=start,success=true    15
    tool=kotlin_tdd_workflow,command=list,success=true      3

[mcp.tool.duration]
    Percentile=50                                       2347
    Percentile=95                                       4521
    Percentile=99                                       5890

[mcp.tool.tokens]
    tool=kotlin_tdd_workflow                          598965
```

### Query Structured Logs
```bash
# View latest entries
tail -f /tmp/mcp_logs/structured.jsonl

# Parse with jq
cat /tmp/mcp_logs/structured.jsonl | jq -r 'select(.Properties.Tool=="kotlin_tdd_workflow")'

# Count by flow
cat structured.jsonl | jq -r '.Properties.Flow' | sort | uniq -c
      15 standard_import_flow
       3 m2m_import_flow
       2 bill_export_flow

# Find errors
cat structured.jsonl | jq 'select(.Level=="Error")'

# Average duration per command
cat structured.jsonl | jq -r '.Properties | select(.Tool) | [.Command, .DurationMs] | @csv'
```

### Log Format
```json
{
  "@t": "2025-10-14T22:30:45.123Z",
  "@mt": "Tool {Tool} completed: command={Command}, flow={Flow}, duration={DurationMs}ms, tokens={Tokens}, success={Success}",
  "@l": "Information",
  "Tool": "kotlin_tdd_workflow",
  "Command": "start",
  "Flow": "standard_import_flow",
  "DurationMs": 2347.5,
  "Tokens": 39811,
  "Success": true
}
```

---

## What's Missing (TODO: Phase 2)

### Testing (4 hours)
- 5 prompt variation tests
- 10 integration tests
- See `TESTING_STRATEGY.md` (to be created)

### Future Enhancements
- Grafana dashboards (export metrics to Prometheus)
- Alert rules (p95 > 10s, error rate > 5%)
- Metrics API endpoint (query from CI/CD)

---

## Metrics vs Logs

**When to use metrics:**
- Performance trends (p50/p95/p99)
- Success/error rates
- Volume tracking (calls/hour)

**When to use logs:**
- Debugging specific failures
- Audit trail (who called what)
- Context-rich investigation

**Use both together:** Metrics alert, logs debug.

---

## Current Status

✅ Metrics instrumentation complete
✅ Structured logging complete
✅ Integration complete
⏳ Testing (next phase)
⏳ Dashboards (when needed)
