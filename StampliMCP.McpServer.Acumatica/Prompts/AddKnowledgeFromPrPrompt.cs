using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class AddKnowledgeFromPrPrompt
{
    [McpServerPrompt(
        Name = "add_knowledge_from_pr",
        Title = "AI-Driven Knowledge Addition from PR"
    )]
    [Description(@"
AI-driven knowledge addition from PR learnings with intelligent duplicate detection and category routing.

This prompt guides you through:
1. Duplicate detection via query_acumatica_knowledge
2. Triage decision (ADD/SKIP/DUPLICATE/BACKLOG)
3. Automatic file updates using Read/Edit tools
4. MCP server rebuild
5. Structured result output

Usage:
- prNumber: PR number (e.g., '456')
- learnings: What was learned from the PR (must include code location: file + line numbers)
")]
    public static ChatMessage[] Execute(
        [Description("PR number (e.g., '#456')")]
        string prNumber,

        [Description("What you learned from this PR - MUST include code location (file path + line numbers)")]
        string learnings
    )
    {
        Serilog.Log.Information("Prompt {Prompt} started: Adding knowledge from PR {PR}",
            "add_knowledge_from_pr", prNumber);

        try
        {
            var promptText = $@"
# Add Knowledge from PR {prNumber}

You are updating the Stampli MCP Acumatica knowledge base based on learnings from a recent PR.

## PR Learning:
{learnings}

---

## WORKFLOW: 4-Step Knowledge Addition

### STEP 1: Duplicate Detection

Call the MCP tool **query_acumatica_knowledge** to check if this knowledge already exists.

Extract key terms from the learning (operation name, entity type, action verb, patterns) and query:
```
query_acumatica_knowledge(query=""<key terms>"", scope=""all"")
```

Analyze results:
- **DUPLICATE** if fuzzy match > 80% (similar operation/troubleshooting exists)
- Otherwise, proceed to Step 2

---

### STEP 2: Triage Decision

Decide the verdict based on these rules:

**DUPLICATE**:
- Knowledge is already documented in existing operation
- Fuzzy match confidence > 80%
- Suggestion: Maybe add detail to existing troubleshooting section

**SKIP**:
- Customer-specific quirk (affects only 1 customer, not general pattern)
- One-time workaround, not Acumatica core behavior
- Edge case that doesn't generalize

**BACKLOG**:
- Interesting general pattern BUT missing code location (no file path + line numbers)
- Cannot add to knowledge without code reference
- Suggestion: Get code location and retry

**ADD**:
- General Acumatica pattern (affects multiple customers)
- Code location provided (file path + line numbers)
- Not a duplicate
- Ready to document

If verdict is ADD, proceed to Step 3. Otherwise, skip to Step 4 (output result).

---

### STEP 3: Add Knowledge to Files (ONLY if verdict = ADD)

You are running in the StampliMCP repository directory: `/mnt/c/Users/Kosta/source/repos/StampliMCP`

#### 3a. Read Documentation
Use **Read tool** to understand structure:
- `StampliMCP.McpServer.Acumatica/Knowledge/KNOWLEDGE_CONTRIBUTING.md` - Category decision tree
- `StampliMCP.McpServer.Acumatica/Knowledge/_operation_template.json` - Operation structure
- `StampliMCP.McpServer.Acumatica/Knowledge/categories.json` - Category registry

#### 3b. Route to Category
Use the decision tree from KNOWLEDGE_CONTRIBUTING.md to determine category:
- Extract entity type (Vendor, Payment, PurchaseOrder, etc.)
- Extract action (export, import, search, void, etc.)
- Apply routing rules

Example:
- ""void payment"" → category = ""payments""
- ""export vendor with custom fields"" → category = ""customFields""
- ""search cost codes"" → category = ""fields""

#### 3c. Generate Operation JSON
Use _operation_template.json as skeleton. REQUIRED fields:
- **method**: Operation name (camelCase)
- **summary**: One-sentence description
- **category**: Category from step 3b
- **searchKeywords**: 8-12 keywords (include typos, aliases, use cases)
- **scanThese**: File location from the learning (file + lines + purpose)

Optional but recommended:
- **flow**: Associated flow name (if applicable)
- **troubleshooting**: Common errors and solutions
- **constants**: Important constants used

#### 3d. Update operations/<category>.json
Use **Read tool** to read current file:
```
StampliMCP.McpServer.Acumatica/Knowledge/operations/<category>.json
```

Use **Edit tool** to add operation to the operations array.

CRITICAL RULES:
- Use ARRAY format: `""operations"": [...]` not object format
- Add your operation object to the end of the array
- Ensure valid JSON (no trailing commas)
- Match indentation of existing operations

#### 3e. Update categories.json Count
Use **Edit tool** on:
```
StampliMCP.McpServer.Acumatica/Knowledge/categories.json
```

Find the category entry and increment the count by 1.

Example: If category is ""payments"" and count was 7, change to 8.

#### 3f. Rebuild MCP Server
Use **Bash tool** to rebuild:

```bash
# Step 1: Kill running MCP server (MANDATORY)
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-acumatica.exe || true

# Step 2: Build
""/mnt/c/Program Files/dotnet/dotnet.exe"" build -c Release --nologo

# Step 3: Publish
""/mnt/c/Program Files/dotnet/dotnet.exe"" publish StampliMCP.McpServer.Acumatica/StampliMCP.McpServer.Acumatica.csproj -c Release -r win-x64 --self-contained /p:PublishSingleFile=true /p:PublishAot=false --nologo --no-build
```

Check exit codes to determine rebuild status (success or failed).

---

### STEP 4: Output Structured Result

Return a JSON object with this structure:

```json
{{
  ""verdict"": ""ADD"" | ""SKIP"" | ""DUPLICATE"" | ""BACKLOG"",
  ""reason"": ""Explanation of why this verdict"",
  ""duplicateOf"": [""operationName1"", ""operationName2""] // if DUPLICATE
  ""category"": ""categoryName"", // if ADD
  ""filesModified"": [""operations/<category>.json"", ""categories.json""], // if ADD
  ""operationAdded"": {{ /* full operation JSON */ }}, // if ADD
  ""rebuildStatus"": ""success"" | ""failed"" | ""skipped"",
  ""suggestion"": ""What user should do next""
}}
```

---

## CRITICAL ENFORCEMENT RULES

✅ **MANDATORY**: Call query_acumatica_knowledge FIRST (prevent duplicates)
✅ **MANDATORY**: Code location (file + lines) required for ADD verdict
✅ **MANDATORY**: Use ARRAY format for operations
✅ **MANDATORY**: Increment categories.json count when adding
✅ **MANDATORY**: Generate searchKeywords (min 5, max 15)
✅ **MANDATORY**: Rebuild MCP server after file changes
✅ **MANDATORY**: Return structured JSON result

---

## Example Execution

**Input**: learnings = ""void-and-reissue flow for checks requires ApprovedForPayment=false. See AcumaticaPaymentHandler.java:234-289""

**Step 1**: query_acumatica_knowledge(""void reissue check payment"")
- Finds: voidPayment operation exists

**Step 2**: Triage
- Verdict: DUPLICATE (voidPayment already documents void logic)
- DuplicateOf: [""voidPayment""]

**Step 4**: Output
```json
{{
  ""verdict"": ""DUPLICATE"",
  ""reason"": ""Void logic already documented in operations/payments.json → voidPayment"",
  ""duplicateOf"": [""voidPayment""],
  ""suggestion"": ""Consider adding ApprovedForPayment requirement to voidPayment troubleshooting section""
}}
```

---

Begin execution now. Good luck!
";

            return new[]
            {
                new ChatMessage(ChatRole.System,
                    "You are an expert knowledge curator for the Acumatica MCP server. " +
                    "You intelligently add knowledge from PR learnings with duplicate detection, " +
                    "category routing, and automatic file updates. You always return structured JSON results."),

                new ChatMessage(ChatRole.User, promptText)
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Prompt {Prompt} failed: {Error}", "add_knowledge_from_pr", ex.Message);

            return new[]
            {
                new ChatMessage(ChatRole.User,
                    $"Error in add_knowledge_from_pr prompt: {ex.Message}")
            };
        }
    }
}
