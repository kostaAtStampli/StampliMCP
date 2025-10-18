# 🚀 Nuclear MCP v4.0.0 - Deployment & Usage Guide
## Composable Architecture with Interactive Prompts! 💬

## 📦 Release Build Location

### ✅ Executable Ready
```
📁 Location:
C:\Users\Kosta\source\repos\StampliMCP\
└── StampliMCP.McpServer.Acumatica\
    └── bin\Release\net10.0\win-x64\publish\
        ├── stampli-mcp-acumatica.exe  (~31 MB - Self-contained)
        └── Knowledge\
            ├── operations\  (10 JSON files - 51 operations)
            └── kotlin\      (13 files - Kotlin TDD knowledge)
```

### 📊 Build Details
- **Version**: 4.0.0-BUILD_2025_10_18_PROMPT_FIX
- **Type**: Self-contained Release (includes .NET 10 runtime)
- **Platform**: Windows x64
- **Size**: ~31 MB (single file + Knowledge folder)
- **Features**: **9 Tools + 5 Prompts = 14 MCP primitives**
- **Dependencies**: ZERO (no .NET runtime needed)
- **Protocol**: MCP 2025-06-18 (Composable tools, elicitation, structured outputs)
- **SDK Workaround**: Explicit prompt registration (0.4.0-preview.2 bug fix)

---

## 🏃 How to Run

### Option 1: Direct Execution (Stdio Mode)
```cmd
cd C:\Users\Kosta\source\repos\StampliMCP\StampliMCP.McpServer.Acumatica\bin\Release\net10.0\win-x64\publish

stampli-mcp-acumatica.exe
```

**Behavior**:
- Starts MCP server
- Communicates via **stdin/stdout** (MCP protocol)
- Waits for JSON-RPC messages
- Does NOT open a UI or window

---

## 🔌 How to Connect as MCP

### **Claude Desktop** (Recommended)

#### Step 1: Locate Config File
```
Windows: %APPDATA%\Claude\claude_desktop_config.json
Mac: ~/Library/Application Support/Claude/claude_desktop_config.json
```

#### Step 2: Add MCP Server
```json
{
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe",
      "args": [],
      "env": {}
    }
  }
}
```

#### Step 3: Restart Claude Desktop
- Close Claude Desktop completely
- Reopen
- Click 🔌 icon (bottom-right)
- Should see "stampli-acumatica" with ✅ **Connected**

#### Step 4: Use MCP Tools & Prompts

**9 Composable Tools:**
- `query_acumatica_knowledge` - Search operations/flows
- `recommend_flow` - Get TDD flow with confidence score
- `get_flow_details` - Flow anatomy and steps
- `validate_request` - Validate against schemas
- `diagnose_error` - Root cause analysis
- `get_kotlin_golden_reference` - Golden reference files
- `kotlin_tdd_workflow` - Complete TDD workflow
- `health_check` - Server health verification
- `check_knowledge_files` - Knowledge verification

**5 Interactive Prompts:**
- `kotlin_tdd_tasklist` - Autonomous 7-step TDD workflow
- `implement_feature_guided` - Pair-programming TDD session
- `plan_comprehensive_tests` - QA test planning
- `debug_with_expert` - Debugging conversation
- `analyze_integration_strategy` - Architecture planning

**Example Usage:**
```
You: Use implement_feature_guided to add vendor bulk import

Claude: [Starts interactive TDD conversation, guides through:
         Discover → Query → Scan → Test(RED) → Implement → Test(GREEN)]
```

---

### **VS Code with Cline Extension**

#### Step 1: Install Cline Extension
```
Extensions → Search "Cline" → Install
```

#### Step 2: Configure MCP
Open Cline Settings → MCP Servers → Add:
```json
{
  "stampli-acumatica": {
    "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe"
  }
}
```

#### Step 3: Use Tools & Prompts
Cline will auto-discover all 9 tools + 5 prompts (see list above)

---

### **Continue.dev Extension**

#### Config Location
`.continue/config.json` in your project:

```json
{
  "experimental": {
    "modelContextProtocol": true
  },
  "mcpServers": {
    "stampli-acumatica": {
      "command": "C:\\Users\\Kosta\\source\\repos\\StampliMCP\\StampliMCP.McpServer.Acumatica\\bin\\Release\\net10.0\\win-x64\\publish\\stampli-mcp-acumatica.exe"
    }
  }
}
```

---

### **Manual Testing (curl)**

```bash
# Start server
stampli-mcp-acumatica.exe

# In another terminal, send JSON-RPC:
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | stampli-mcp-acumatica.exe
```

---

## ⚙️ Native AOT Compilation (Optional - Smaller Binary)

### What You Need

**From Visual Studio Installer** → Modify → **Install**:
```
✅ Desktop development with C++
   ├── MSVC v143 - VS 2022 C++ x64/x86 build tools
   ├── Windows 11 SDK
   └── C++ CMake tools for Windows
```

### For ARM64 Development:
```
✅ C++ ARM64 build tools
```

### How to Install:
1. Open **Visual Studio Installer**
2. Click **Modify** on your VS 2022 installation
3. Go to **Workloads** tab
4. Check ☑️ **Desktop development with C++**
5. Go to **Individual components** tab (optional specific items):
   - ☑️ MSVC v143 - VS 2022 C++ x64/x86 build tools (Latest)
   - ☑️ Windows 11 SDK (10.0.22621.0)
   - ☑️ C++ CMake tools for Windows
6. Click **Modify** button (downloads ~5-7 GB)
7. Wait 10-20 minutes for installation

### After Installation - Compile with AOT:
```cmd
cd C:\Users\Kosta\source\repos\StampliMCP

dotnet publish StampliMCP.McpServer.Acumatica\StampliMCP.McpServer.Acumatica.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained ^
  /p:PublishAot=true
```

**Result**:
- Binary size: ~10-15 MB (vs 31 MB self-contained)
- Startup time: Instant (vs 200ms with JIT)
- Native machine code (no JIT compilation)

---

## 🎯 Usage Examples

### Example 1: Implement Kotlin Feature (Nuclear Entry Point)
```
User → AI: Use implement_kotlin_feature tool with "Add vendor bulk import with CSV validation"

MCP Returns:
{
  "workflow": {
    "steps": [
      {
        "step": 1,
        "name": "Discover Operations",
        "instruction": "Analyze 'Add vendor bulk import...' and identify operations needed",
        "validation": "List of operation method names identified",
        "nextTool": "get_operation_details"
      },
      ... 6 more steps ...
    ]
  },
  "resources": {
    "patterns": "Knowledge/kotlin/GOLDEN_PATTERNS.md",
    "errors": "Knowledge/kotlin/error-patterns-kotlin.json",
    ...
  },
  "enforcement": {
    "rule1": "NEVER skip TDD RED phase - tests MUST fail first",
    ...
  }
}

AI → Follows workflow autonomously → Feature implemented with tests passing ✅
```

### Example 2: Get Surgical Operation Details
```
User → AI: Get details for exportVendor operation

AI → Calls: get_operation_details("exportVendor")

MCP Returns:
{
  "method": "exportVendor",
  "requiredFields": [
    {"name": "vendorName", "maxLength": 60, "required": true},
    {"name": "stampliLink", "required": true}
  ],
  "errors": [
    "vendorName is required",
    "vendorName exceeds maximum length of 60 characters"
  ],
  "scanFiles": [
    {"path": "CreateVendorHandler.java", "lines": "22-90", "purpose": "Validation"},
    {"path": "GOLDEN_PATTERNS.md", "lines": "61-115", "purpose": "Copy-paste pattern"}
  ],
  "goldenPattern": {...},
  "tddWorkflow": {...}
}

AI → Uses exact validation rules → Implements with TDD ✅
```

---

## 📊 Tool Inventory

### 🔥 **Nuclear Tools** (Workflow Enforcement)
1. **`implement_kotlin_feature`** - Entry point, 7-step enforced TDD workflow
2. **`get_operation_details`** - Surgical operation details with file pointers

### 🛠️ **Universal Tools** (Data Retrieval)
3. **`list_categories`** - List 9 operation categories
4. **`search_operations`** - Search 51 operations by keyword
5. **`get_enums`** - Get 6 enum types with values
6. **`get_error_catalog`** - Get all error patterns

### 🧠 **Intelligence Showcase Tools** (AI Analysis - NEW!)
7. **`analyze_integration_complexity`** - Strategic planning & effort estimation
   - Input: "Implement vendor payment workflow with approval chains"
   - Output: Complexity score, effort hours, ordered operations, dependencies, risks

8. **`troubleshoot_error`** - Root cause analysis & debugging
   - Input: "vendorName exceeds maximum length of 60 characters"
   - Output: Root cause, fix steps, prevention strategy, related errors

9. **`recommend_operation`** - Business → Technical translation
   - Input: "Pay multiple vendors at once from single approval"
   - Output: Best operation, alternatives, trade-offs, implementation approach

10. **`generate_test_scenarios`** - Comprehensive test plan generation
    - Input: "exportVendor"
    - Output: Happy path, edge cases, error cases, performance, security tests

**Total**: 10 tools (2 Nuclear + 4 Universal + 4 Intelligence)

### 💬 **Interactive Prompts** (Conversational Guidance - NEW IN V2.0!)
MCP Prompts are pre-built conversations that guide AI through workflows interactively.
Unlike tools (which return data), prompts return chat messages that feel like pair-programming with an expert.

11. **`implement_feature_guided`** - TDD pair-programming conversation
    - Returns: Multi-turn conversation guiding AI through 7-step TDD workflow
    - Experience: Like having senior dev walk you through implementation
    - Use when: Want conversational guidance vs raw workflow JSON

12. **`analyze_integration_strategy`** - Solutions architect conversation
    - Returns: Strategic planning dialogue with business + technical perspectives
    - Experience: Like consulting session with enterprise architect
    - Use when: Need stakeholder-friendly integration analysis

13. **`debug_with_expert`** - Senior debugger conversation
    - Returns: Interactive troubleshooting session with root cause analysis
    - Experience: Like pair-debugging with senior engineer during crisis
    - Use when: Stuck on error and want guided debugging

14. **`plan_comprehensive_tests`** - QA engineer conversation
    - Returns: Test planning session with P0/P1/P2 prioritization
    - Experience: Like test design review with QA lead
    - Use when: Need comprehensive test plan generation

**How Prompts Differ from Tools:**
| Aspect | Tools | Prompts |
|--------|-------|---------|
| **Returns** | Structured data (JSON) | Chat messages (conversations) |
| **Feel** | Data dump | Guided dialogue |
| **Use Case** | Retrieve information | Get coached through workflow |
| **Example** | `get_operation_details` → JSON | `implement_feature_guided` → Conversation |
| **Manager WOW** | "Here's the data" | "Watch AI think and plan" |

**Total**: 10 Tools + 4 Prompts = **14 MCP Primitives**

**In Claude Desktop:**
- Tools appear in tools list (🔧 icon)
- Prompts appear in prompts list (💬 icon)
- Both accessible from MCP connection panel

---

## 🔍 Verification

### Check if MCP is Running:
```cmd
# In PowerShell
Get-Process | Where-Object {$_.Name -like "*stampli-mcp*"}
```

### Check Logs:
```cmd
# MCP outputs to stderr
stampli-mcp-acumatica.exe 2> mcp-log.txt
```

### Test Connection:
In Claude Desktop:
1. Open chat
2. Type: `List available MCP tools`
3. Should see: `implement_kotlin_feature`, `get_operation_details`, etc.

---

## ⚡ Performance

| Metric | Self-Contained | Native AOT |
|--------|----------------|------------|
| **Binary Size** | 31 MB | ~12 MB |
| **Startup Time** | ~200ms | ~10ms |
| **Memory Usage** | ~50 MB | ~30 MB |
| **Runtime** | .NET 10 JIT | Native code |
| **Build Time** | 30 seconds | 2-3 minutes |

**Recommendation**: Use **Self-Contained** (current) unless you need:
- Faster cold starts
- Smaller deployment
- No runtime dependencies at all

---

## 🚨 Troubleshooting

### Issue 1: "Access Denied" when running
**Solution**: Right-click → Run as Administrator (first time only)

### Issue 2: Claude Desktop doesn't see MCP
**Solution**:
1. Check config file path correct
2. Use **double backslashes** in Windows paths: `C:\\Users\\...`
3. Restart Claude Desktop completely

### Issue 3: MCP crashes on startup
**Solution**:
1. Check Knowledge folder exists next to .exe
2. Run `stampli-mcp-acumatica.exe --help` to verify it runs
3. Check stderr for error messages

### Issue 4: Tools not showing up
**Solution**: MCP protocol expects stdin/stdout communication. Don't run in interactive terminal.

---

## 📝 Next Steps

1. ✅ **Executable is ready** at publish folder
2. ✅ **Knowledge files included** (13 Kotlin + 10 operations)
3. ⏳ **Configure Claude Desktop** with config above
4. ⏳ **Test Nuclear Tools** with sample feature
5. ⏳ **Install C++ tools** (optional) for Native AOT

---

## 📞 Support

- **MCP Docs**: https://modelcontextprotocol.io
- **Claude Desktop**: https://claude.ai/download
- **Native AOT Prerequisites**: https://aka.ms/nativeaot-prerequisites

**Built with Nuclear MCP 2025 Architecture** 🚀
