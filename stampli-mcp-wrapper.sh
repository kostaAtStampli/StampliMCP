#!/bin/bash
# Wrapper script for Stampli MCP Acumatica Server
# Suppresses info/debug logs to prevent interference with JSON-RPC communication

# Path to the actual executable
EXEC_PATH="/mnt/c/Users/Kosta/source/repos/StampliMCP/StampliMCP.McpServer.Acumatica/bin/Release/net10.0/win-x64/publish/stampli-mcp-acumatica.exe"

# Set environment variable to suppress console logging
export Logging__Console__LogLevel__Default=None
export Logging__LogLevel__Default=None
export DOTNET_ENVIRONMENT=Production

# Run the executable with clean stdio for JSON-RPC
exec "$EXEC_PATH" "$@"