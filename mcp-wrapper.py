#!/usr/bin/env python3
"""
MCP Server Wrapper for StampliMCP Acumatica
Fixes StdioServerTransport logging issues by filtering out info logs
"""

import sys
import subprocess
import os
import json
import threading
from queue import Queue

def filter_logs(line):
    """Filter out log lines, only pass through JSON-RPC messages"""
    try:
        # Try to parse as JSON - if successful, it's likely JSON-RPC
        json.loads(line)
        return True
    except:
        # Not JSON, likely a log line
        # Log lines typically start with info:, warn:, error:, etc.
        if line.strip() and not any(line.startswith(prefix) for prefix in ['info:', 'warn:', 'error:', 'dbug:', 'fail:', 'crit:']):
            # Might be part of a multi-line JSON
            return True
        return False

def forward_stream(input_stream, output_stream, should_filter=False):
    """Forward data from input to output, optionally filtering"""
    for line in iter(input_stream.readline, b''):
        if not line:
            break

        line_str = line.decode('utf-8', errors='ignore')

        if should_filter:
            if filter_logs(line_str):
                output_stream.write(line)
                output_stream.flush()
        else:
            output_stream.write(line)
            output_stream.flush()

def main():
    # Path to the actual executable
    exe_path = "/mnt/c/Users/Kosta/source/repos/StampliMCP/StampliMCP.McpServer.Unified/bin/Debug/net10.0/stampli-mcp-unified.dll"

    # Set environment to suppress console logging
    env = os.environ.copy()
    env['Logging__Console__LogLevel__Default'] = 'None'
    env['DOTNET_ENVIRONMENT'] = 'Production'

    # Start the subprocess
    process = subprocess.Popen(
        [exe_path] + sys.argv[1:],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,  # Discard stderr (logs)
        env=env
    )

    # Forward stdin to the subprocess
    stdin_thread = threading.Thread(
        target=forward_stream,
        args=(sys.stdin.buffer, process.stdin, False)
    )
    stdin_thread.daemon = True
    stdin_thread.start()

    # Forward stdout from subprocess, filtering logs
    stdout_thread = threading.Thread(
        target=forward_stream,
        args=(process.stdout, sys.stdout.buffer, True)
    )
    stdout_thread.daemon = True
    stdout_thread.start()

    # Wait for the process to complete
    process.wait()
    sys.exit(process.returncode)

if __name__ == '__main__':
    main()
