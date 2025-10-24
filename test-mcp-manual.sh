#!/bin/bash

# Manual test script to debug Claude CLI + MCP interaction

echo "=== Testing Claude CLI with MCP Server ==="
echo ""

# Create test directory
TEST_DIR="/mnt/c/Users/Kosta/AppData/Local/Temp/manual_mcp_test_$$"
mkdir -p "$TEST_DIR"
echo "Test directory: $TEST_DIR"

# Create MCP config
cat > "$TEST_DIR/.claude-code-config.json" <<'EOF'
{
  "mcpServers": {
    "stampli_unified": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Unified\\StampliMCP.McpServer.Unified.csproj"
      ],
      "env": {
        "MCP_DEBUG": "true"
      }
    }
  }
}
EOF

echo "MCP config created"
echo ""

# Test 1: Simple prompt with timeout
echo "=== Test 1: List tools with 30s timeout ==="
cd "$TEST_DIR"

# Run claude with timeout, auto-answer "2" if prompted
timeout 30s bash -c 'echo "2" | claude --mcp-config "'$TEST_DIR'/.claude-code-config.json" --print --permission-mode bypassPermissions "List available MCP tools"' 2>&1 | head -100

EXIT_CODE=$?
echo ""
echo "Exit code: $EXIT_CODE (124 = timeout, 0 = success)"
echo ""

# Check what files were created
echo "=== Files created ==="
find "$TEST_DIR" -type f

echo ""
echo "=== Test complete ==="
echo "Cleanup: rm -rf $TEST_DIR"
