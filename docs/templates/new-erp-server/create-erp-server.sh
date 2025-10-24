#!/bin/bash

# Scaffolding script to create a new ERP MCP server from templates
# Usage: ./create-erp-server.sh QuickBooks

set -e

if [ -z "$1" ]; then
    echo "Usage: ./create-erp-server.sh <ErpName>"
    echo "Example: ./create-erp-server.sh QuickBooks"
    exit 1
fi

ERP_NAME="$1"
ERP_NAME_LOWER=$(echo "$ERP_NAME" | tr '[:upper:]' '[:lower:]')
REPO_ROOT="/mnt/c/Users/Kosta/source/repos/StampliMCP"
TEMPLATE_DIR="$REPO_ROOT/docs/templates/new-erp-server"
TARGET_DIR="$REPO_ROOT/StampliMCP.McpServer.$ERP_NAME"

echo "==========================================="
echo "Creating new ERP MCP Server: $ERP_NAME"
echo "==========================================="

# Step 1: Create project directory structure
echo "Step 1: Creating directory structure..."
mkdir -p "$TARGET_DIR/Services"
mkdir -p "$TARGET_DIR/Tools"
mkdir -p "$TARGET_DIR/Models"
mkdir -p "$TARGET_DIR/Knowledge/operations"
mkdir -p "$TARGET_DIR/Knowledge/flows"

# Step 2: Generate .csproj from template
echo "Step 2: Generating .csproj..."
sed "s/{ErpName}/$ERP_NAME/g; s/{ErpNameLower}/$ERP_NAME_LOWER/g" \
    "$TEMPLATE_DIR/StampliMCP.McpServer.{ErpName}.csproj.template" \
    > "$TARGET_DIR/StampliMCP.McpServer.$ERP_NAME.csproj"

# Step 3: Generate Program.cs from template
echo "Step 3: Generating Program.cs..."
sed "s/{ErpName}/$ERP_NAME/g; s/{ErpNameLower}/$ERP_NAME_LOWER/g" \
    "$TEMPLATE_DIR/Program.cs.template" \
    > "$TARGET_DIR/Program.cs"

# Step 4: Generate KnowledgeService from template
echo "Step 4: Generating ${ERP_NAME}KnowledgeService.cs..."
sed "s/{ErpName}/$ERP_NAME/g" \
    "$TEMPLATE_DIR/{ErpName}KnowledgeService.cs.template" \
    > "$TARGET_DIR/Services/${ERP_NAME}KnowledgeService.cs"

# Step 5: Generate FlowService from template
echo "Step 5: Generating ${ERP_NAME}FlowService.cs..."
sed "s/{ErpName}/$ERP_NAME/g" \
    "$TEMPLATE_DIR/{ErpName}FlowService.cs.template" \
    > "$TARGET_DIR/Services/${ERP_NAME}FlowService.cs"

# Step 6: Create minimal categories.json
echo "Step 6: Creating categories.json..."
cat > "$TARGET_DIR/Knowledge/categories.json" <<EOF
{
  "categories": [
    {
      "name": "general",
      "count": 0,
      "description": "General $ERP_NAME operations"
    }
  ]
}
EOF

# Step 7: Create minimal operation example
echo "Step 7: Creating example operation..."
cat > "$TARGET_DIR/Knowledge/operations/general.json" <<EOF
{
  "operations": []
}
EOF

# Step 8: Create README for the new server
echo "Step 8: Creating README.md..."
cat > "$TARGET_DIR/README.md" <<EOF
# StampliMCP.McpServer.$ERP_NAME

MCP server for $ERP_NAME integration.

## Build

\`\`\`bash
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release --nologo
\`\`\`

## Publish

\`\`\`bash
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \\
  StampliMCP.McpServer.$ERP_NAME/StampliMCP.McpServer.$ERP_NAME.csproj \\
  -c Release -r win-x64 --self-contained /p:PublishSingleFile=true \\
  /p:PublishAot=false --nologo --no-build
\`\`\`

## Knowledge Structure

- \`Knowledge/categories.json\` - Defines operation categories
- \`Knowledge/operations/*.json\` - Operation definitions by category
- \`Knowledge/flows/*.json\` - Integration flow patterns

## Next Steps

1. Add your operations to \`Knowledge/operations/general.json\`
2. Create flow definitions in \`Knowledge/flows/\`
3. Update \`${ERP_NAME}KnowledgeService.cs\` CategoryFileMapping
4. Implement flow matching logic in \`${ERP_NAME}FlowService.cs\`
5. Create MCP tools in \`Tools/\` directory
EOF

# Step 9: Add to solution (if .sln exists)
if [ -f "$REPO_ROOT/StampliMCP.sln" ]; then
    echo "Step 9: Adding project to solution..."
    cd "$REPO_ROOT"
    dotnet sln StampliMCP.sln add "$TARGET_DIR/StampliMCP.McpServer.$ERP_NAME.csproj" || true
fi

echo ""
echo "==========================================="
echo "SUCCESS! Created $ERP_NAME MCP server at:"
echo "$TARGET_DIR"
echo "==========================================="
echo ""
echo "Next steps:"
echo "1. cd $TARGET_DIR"
echo "2. Add your first operation to Knowledge/operations/general.json"
echo "3. Build: dotnet build -c Release"
echo "4. Customize ${ERP_NAME}KnowledgeService and ${ERP_NAME}FlowService"
echo ""
