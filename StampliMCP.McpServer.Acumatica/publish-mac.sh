#!/usr/bin/env bash
#
# publish-mac.sh
# Publishes the Stampli MCP Acumatica server as a self-contained macOS executable with Native AOT
#
# NOTE: This script must be run on a macOS machine due to Native AOT cross-compilation limitations.
# For cross-platform builds without AOT, use publish-mac-portable.sh

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION="${1:-Release}"
RUNTIME="${2:-osx-arm64}" # Default to Apple Silicon, can be osx-x64 for Intel
OUTPUT_PATH="./publish/${RUNTIME}"

echo -e "${CYAN}🚀 Publishing Stampli MCP Acumatica Server for macOS${NC}"
echo -e "${YELLOW}Configuration: ${CONFIGURATION}${NC}"
echo -e "${YELLOW}Runtime: ${RUNTIME}${NC}"
echo -e "${YELLOW}Output Path: ${OUTPUT_PATH}${NC}"

# Detect architecture
if [[ $(uname -m) == "arm64" ]]; then
    echo -e "${GREEN}✓ Running on Apple Silicon Mac${NC}"
    if [[ "$RUNTIME" == "osx-x64" ]]; then
        echo -e "${YELLOW}⚠️  Cross-compiling from ARM64 to x64${NC}"
    fi
elif [[ $(uname -m) == "x86_64" ]]; then
    echo -e "${GREEN}✓ Running on Intel Mac${NC}"
    if [[ "$RUNTIME" == "osx-arm64" ]]; then
        echo -e "${YELLOW}⚠️  Cross-compiling from x64 to ARM64${NC}"
    fi
fi

# Clean previous build
if [ -d "$OUTPUT_PATH" ]; then
    echo -e "${CYAN}Cleaning previous build...${NC}"
    rm -rf "$OUTPUT_PATH"
fi

# Restore dependencies
echo -e "\n${CYAN}📦 Restoring dependencies...${NC}"
dotnet restore

# Build the project
echo -e "\n${CYAN}🔨 Building project...${NC}"
dotnet build -c "$CONFIGURATION" --no-restore

# Publish with Native AOT
echo -e "\n${CYAN}📤 Publishing with Native AOT...${NC}"
dotnet publish \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    -o "$OUTPUT_PATH" \
    --self-contained \
    /p:PublishAot=true \
    /p:PublishSingleFile=true \
    /p:OptimizationPreference=Size \
    /p:DebugType=None \
    /p:DebugSymbols=false

# Check if build was successful
EXECUTABLE_PATH="${OUTPUT_PATH}/stampli-mcp-acumatica"
if [ -f "$EXECUTABLE_PATH" ]; then
    # Make executable
    chmod +x "$EXECUTABLE_PATH"

    # Get file size
    SIZE_BYTES=$(stat -f%z "$EXECUTABLE_PATH" 2>/dev/null || stat -c%s "$EXECUTABLE_PATH" 2>/dev/null || echo "0")
    SIZE_MB=$(echo "scale=2; $SIZE_BYTES / 1048576" | bc)

    echo -e "\n${GREEN}✅ Build successful!${NC}"
    echo -e "Executable: ${EXECUTABLE_PATH}"
    echo -e "Size: ${SIZE_MB}MB"

    # Show how to test the MCP server
    echo -e "\n${CYAN}📝 To test the MCP server:${NC}"
    echo -e "${YELLOW}  ${EXECUTABLE_PATH}${NC}"
    echo -e "  This will start the MCP server using stdio transport"

    echo -e "\n${CYAN}🎯 To use with Claude Desktop on macOS:${NC}"
    echo -e "  Add to ~/Library/Application Support/Claude/claude_desktop_config.json:"
    echo -e '  {
    "mcpServers": {
      "stampli-acumatica": {
        "command": "'$(realpath "$EXECUTABLE_PATH")'"
      }
    }
  }'
else
    echo -e "${RED}❌ Build failed: Executable not found at ${EXECUTABLE_PATH}${NC}"
    exit 1
fi