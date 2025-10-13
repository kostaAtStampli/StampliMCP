#!/bin/bash
# Quick test to show Claude CLI response

echo "========================================="
echo "Test 1: Simple Math Question"
echo "========================================="
~/.local/bin/claude --print --dangerously-skip-permissions "What is 2+2?"

echo ""
echo "========================================="
echo "Test 2: MCP Tools Query"
echo "========================================="
~/.local/bin/claude --print --dangerously-skip-permissions "List all available MCP tools from stampli-acumatica server"
