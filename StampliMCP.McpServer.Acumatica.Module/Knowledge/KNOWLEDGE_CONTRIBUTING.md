# Knowledge Contributing Guide

**Version**: 1.0.0
**Target Audience**: AI agents adding new Acumatica operation knowledge
**Last Updated**: 2025-10-20

---

## Quick Start for AI

When user says: *"I implemented [operationName], update MCP knowledge with [details]"*

**Follow this checklist:**
1. ✅ Determine category using decision tree below
2. ✅ Use `_operation_template.json` as skeleton
3. ✅ Add operation to appropriate `operations/{category}.json` in **ARRAY format**
4. ✅ Update `categories.json` count for that category
5. ✅ Add searchKeywords for fuzzy matching
6. ✅ Remind user to rebuild (commands in Rebuild Workflow section)

---

## Category Decision Tree

### Step 1: What type of operation is this?

```
┌─ Vendor operations (create/read/update/search/delete)
│  → operations/vendors.json
│
┌─ Item operations (search items, get categories)
│  → operations/items.json
│
┌─ Purchase Order operations (search POs, get items per PO, export PO, PO matching)
│  → operations/purchaseOrders.json
│
┌─ Payment operations (export payment/check, void, get paid bills, retrieve credits/bills)
│  → operations/payments.json
│
┌─ Account operations (GL accounts, bank accounts, payable accounts, discount accounts)
│  → operations/accounts.json
│
┌─ Field/Class operations (cost codes, job numbers, units, classes, customers, filtering fields)
│  → operations/fields.json
│
┌─ Custom field operations (ONLY $adHocSchema discovery + export with Attribute* DAC)
│  → custom-field-operations.json (root level, special file)
│  ⚠️  NOT for getCustomFieldSearchList (that's in fields.json)
│
┌─ Admin operations (connect, disconnect, ping, config, validate, extension version)
│  → operations/admin.json
│
┌─ Retrieval operations (duplicate checks)
│  → operations/retrieval.json
│
┌─ Utility operations (debugging, testing tools)
│  → operations/utility.json
│
└─ Other operations (exportAPTransaction router, exportInvoice internal method)
   → operations/other.json
```

### Step 2: Unsure which category?

**Ask yourself:**
- Is this a CRUD operation on a specific entity? → Use entity name (vendors, items, purchaseOrders)
- Is this searching/importing master data? → Check if entity has dedicated category, else `fields`
- Is this an admin/config operation? → `admin`
- Is this exporting WITH custom fields (Attribute* DAC)? → `custom-field-operations.json`
- Is this a router/internal method with no AgentOpCode? → `other`

**Still unsure?**
- Scan existing categories in `categories.json` for closest match
- Prefer EXISTING category over creating new one
- Only create new category if operation count would exceed 12-15 in existing file

---

## File Format Standard

### ⚠️ MANDATORY: Use ARRAY Format

All operation files MUST use array format:

```json
{
  "category": "categoryName",
  "note": "Optional context about this category",
  "operations": [
    {
      "method": "operationName",
      "summary": "Description...",
      ...
    },
    {
      "method": "anotherOperation",
      ...
    }
  ]
}
```

**❌ DO NOT use object format:**
```json
{
  "operations": {
    "operationName": {...},  ← WRONG
    "anotherOperation": {...}
  }
}
```

**Why array?**
- Easier for AI to append new operations
- Consistent across all operations/ files
- Parser supports both, but array is standard

**Exception:** `custom-field-operations.json` currently uses object format but will be migrated to array.

---

## Required vs Optional Fields

### ✅ REQUIRED Fields (every operation must have)

1. **`method`** (string) - Operation name matching Java/Kotlin method name
   - Example: `"exportVendor"`, `"getVendors"`, `"voidPayment"`

2. **`summary`** (string) - One-sentence description of what operation does
   - Example: `"Creates or updates vendor in Acumatica with risk control checks"`
   - Keep it concise (1-2 lines), not a paragraph

3. **`category`** (string) - Must match category name from `categories.json`
   - Example: `"vendors"`, `"payments"`, `"admin"`

### ⭐ HIGHLY RECOMMENDED Fields

4. **`searchKeywords`** (array of strings) - For fuzzy matching in query tool
   - Include: common typos, aliases, related terms
   - Example: `["vendor", "supplier", "baccount", "vendor export", "create vendor"]`
   - AI will use these for `query_acumatica_knowledge` tool

5. **`flow`** (string or null) - Which integration flow this uses
   - Options: `"vendor_export_flow"`, `"payment_flow"`, `"standard_import_flow"`, `"po_matching_flow"`, etc.
   - Use `null` if operation doesn't fit existing flow
   - See `payment-flows.json` for full list

6. **`flowTrace`** (array of objects) - Step-by-step execution trace through codebase
   - **When to include:** For EXPORT/CREATE operations, complex flows
   - **Format:**
     ```json
     [
       {
         "layer": "Service Entry|Router|Driver Entry|Handler|Payload Mapper|API Call|Validator",
         "file": "relative/path/from/STAMPLI4/core/src/main/java/...",
         "lines": "123-456",
         "what": "What happens at this step"
       }
     ]
     ```
   - **Example layers:**
     - `"Service Entry"` - BridgeSynchronizationAgent.java
     - `"Router"` - DualBridgeSaaSRouter.java
     - `"Driver Entry"` - AcumaticaDriver.java
     - `"Handler"` - CreateVendorHandler.java (Kotlin) or inline method (Java)
     - `"Payload Mapper"` - VendorPayloadMapper.java, AcumaticaInvoiceSerializer.java
     - `"API Call"` - RestApiCaller.java, AcumaticaAuthenticator wrapper

7. **`scanThese`** (array of objects) - Critical files to read when implementing/debugging
   - **When to include:** ALWAYS for operations with Java/Kotlin implementation
   - **Format:**
     ```json
     [
       {
         "file": "finsys-drivers/acumatica/src/main/java/com/stampli/driver/AcumaticaDriver.java",
         "lines": "386-398",
         "purpose": "Main export method entry point"
       },
       {
         "file": "finsys-drivers/acumatica/src/main/java/com/stampli/driver/vendor/CreateVendorHandler.java",
         "lines": "22-90",
         "purpose": "Validation and JSON payload building"
       }
     ]
     ```
   - **Purpose:** AI can read these files to understand implementation details

### 📋 OPTIONAL Fields (add when available)

8. **`enumName`** (string or null) - AgentOpCode enum name
   - Example: `"EXPORT_VENDOR"`, `"GET_VENDORS"`
   - Use `null` if operation not in AgentOpCode (POC methods)

9. **`requiredFields`** (object) - Required parameters for this operation
   - **Format:**
     ```json
     {
       "vendorName": { "type": "string", "maxLength": 60 },
       "stampliurl": { "type": "string", "aliases": ["stampliUrl", "stampliLink"] }
     }
     ```

10. **`optionalFields`** (object) - Optional parameters
    - Same format as requiredFields

11. **`helpers`** (array of objects) - Helper classes/utilities used
    - **Format:**
      ```json
      [
        {
          "class": "CreateVendorHandler",
          "location": {
            "file": "finsys-drivers/acumatica/.../CreateVendorHandler.java",
            "lines": "22-90"
          },
          "purpose": "Main handler - validates, builds payload, makes API call"
        }
      ]
      ```

12. **`goldenTest`** (object) - Integration test demonstrating operation
    - **Format:**
      ```json
      {
        "file": "finsys-drivers/acumatica/src/test/java/.../AcumaticaDriverCreateVendorITest.java",
        "lines": "30-300",
        "purpose": "Full integration test with ENV1"
      }
      ```

13. **`apiEndpoint`** (object) - Acumatica REST API endpoint details
    - **Format:**
      ```json
      {
        "entity": "Vendor",
        "method": "PUT",
        "urlPattern": "https://{hostname}/entity/{apiVersion}/Vendor"
      }
      ```

14. **`kotlinFlowSummary`** (string) - Kotlin-specific implementation notes
    - Example: `"Uses Handler pattern with CreateVendorHandler.handle() delegation"`

15. **`requestDtoLocation`** (object) - Where to find request DTO definition
    - **Format:**
      ```json
      {
        "file": "bridge/bridge-financial-system/src/main/java/.../ExportVendorRequest.java",
        "lines": "15-45",
        "purpose": "See full DTO structure, fields, validation method"
      }
      ```

16. **`responseDtoLocation`** (object) - Where to find response DTO definition

17. **`constants`** (object) - Important constants used by this operation
    - **Format:**
      ```json
      {
        "MAX_VENDOR_NAME_LENGTH": 60,
        "DEFAULT_VENDOR_CLASS": "STANDARD"
      }
      ```

18. **`validationRules`** (array of strings) - Business logic validation rules
    - Example: `["VendorID must be <= 15 characters", "VendorName required and <= 60 chars"]`

19. **`codeExamples`** (array of objects) - Code snippets showing usage
    - **Format:**
      ```json
      [
        {
          "language": "java",
          "snippet": "CreateVendorHandler handler = new CreateVendorHandler();\nExportResponse response = handler.handle(request);",
          "explanation": "Basic handler invocation pattern"
        }
      ]
      ```

20. **`relatedOperations`** (array of strings) - Operations that work together
    - Example: `["getVendors", "exportVendor"]` for vendor CRUD flow

21. **`isPublicApi`** (boolean) - True if this is a public entry point
    - Example: `exportAPTransaction` is public, `exportInvoice` is internal

22. **`isRouter`** (boolean) - True if this method routes to other operations
    - Example: `exportAPTransaction` routes based on TransactionType

23. **`isInternal`** (boolean) - True if this is an internal implementation detail
    - Example: `exportInvoice` called by `exportAPTransaction`

---

## Field Descriptions: flowTrace vs scanThese vs helpers

**Confused about these three? Here's the difference:**

### `flowTrace` - Execution Path
**Purpose:** Show the EXECUTION PATH when operation runs
**Think:** Call stack from entry point to API call
**Order:** Sequential (Step 1 → Step 2 → Step 3 → ...)
**Example:**
```
1. BridgeSynchronizationAgent.java:514 - Builds DTO
2. DualBridgeSaaSRouter.java:66 - Routes to driver
3. AcumaticaDriver.java:386 - Main entry point
4. CreateVendorHandler.java:22 - Handler logic
5. VendorPayloadMapper.java:11 - JSON serialization
6. RestApiCaller.java:48 - HTTP PUT call
```

### `scanThese` - Key Files to Read
**Purpose:** Tell AI which files to READ to understand implementation
**Think:** "If you want to implement this, read THESE files"
**Order:** By importance (most critical first)
**Example:**
```
1. AcumaticaDriver.java:386-398 - Main method
2. CreateVendorHandler.java:22-90 - Core logic
3. VendorPayloadMapper.java:11-80 - JSON mapping
```

### `helpers` - Utility Classes
**Purpose:** List HELPER/UTILITY classes that assist this operation
**Think:** "These are the support classes used by this operation"
**Order:** Alphabetical or by layer
**Example:**
```
- CreateVendorHandler (main handler)
- VendorPayloadMapper (serialization)
- AcumaticaAuthenticator (auth wrapper)
- VendorRiskControl (duplicate check)
```

**When to use each:**
- **Always include scanThese** - AI needs to know what to read
- **Include flowTrace for complex operations** (exports, payments, multi-step flows)
- **Include helpers when there are 3+ utility classes** involved

---

## searchKeywords Strategy

**Purpose:** Enable fuzzy matching in `query_acumatica_knowledge` tool

### What to Include

1. **Operation name** (exact + common typos)
   - `"exportVendor"` → `["export vendor", "exprt vendor", "export vendro"]`

2. **Entity name** (exact + aliases)
   - Vendor → `["vendor", "supplier", "baccount"]`
   - Invoice → `["invoice", "bill", "ap document"]`
   - Payment → `["payment", "check", "quick check"]`

3. **Action verbs** (exact + synonyms)
   - Export → `["export", "create", "push", "send"]`
   - Import → `["import", "get", "fetch", "retrieve", "pull"]`
   - Void → `["void", "cancel", "reverse"]`

4. **Technical terms** (DACs, endpoints, concepts)
   - `["baccount dac", "vendor entity", "api endpoint"]`

5. **Use cases** (natural language queries)
   - `["how to create vendor", "vendor export", "push vendor to acumatica"]`

### Example
```json
"searchKeywords": [
  "export vendor",
  "create vendor",
  "exprt vendro",
  "vendor export",
  "baccount",
  "supplier",
  "push vendor",
  "vendor create",
  "stampli link",
  "risk control",
  "duplicate vendor check"
]
```

**Tip:** Include common misspellings but don't go overboard (5-15 keywords is ideal)

---

## Updating categories.json

### When to Update Count

**ALWAYS update count when:**
- Adding new operation to category → increment count
- Removing operation from category → decrement count

### How to Update

1. **Find the category:**
```json
{
  "categories": [
    { "name": "vendors", "count": 4, "description": "..." },
    { "name": "payments", "count": 7, "description": "..." }
  ]
}
```

2. **Increment count:**
```json
{ "name": "vendors", "count": 5, "description": "..." }  ← Was 4, now 5
```

3. **DO NOT update description** unless category purpose changes

### Creating New Category

**Only if:**
- Existing category would exceed 12-15 operations
- Operations form distinct logical group
- No existing category fits

**Steps:**
1. Create `operations/{newCategory}.json` with array format
2. Add entry to `categories.json`:
   ```json
   { "name": "newCategory", "count": 1, "description": "Clear description" }
   ```
3. Update `KnowledgeService.cs` lines 75-88 to map category name to file:
   ```csharp
   "newCategory" => "operations/newCategory.json",
   ```

**⚠️ Warning:** Creating new category requires C# code change + rebuild!

---

## Rebuild Workflow

### ⚠️ CRITICAL: Always Rebuild After Knowledge Changes

**Why?** Knowledge files are **embedded resources** compiled into the .NET assembly.
**Result:** Editing JSON won't take effect until rebuild + republish.

### Step-by-Step Rebuild Commands

```bash
# Step 1: Kill running MCP server (MANDATORY - exe gets locked)
/mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-unified.exe

# Step 2: Build (compiles + embeds resources)
"/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release --nologo

# Step 3: Publish (creates self-contained single-file exe)
"/mnt/c/Program Files/dotnet/dotnet.exe" publish \
  StampliMCP.McpServer.Unified/StampliMCP.McpServer.Unified.csproj \
  -c Release -r win-x64 --self-contained \
  /p:PublishSingleFile=true /p:PublishAot=false --nologo --no-build

# Step 4: Reconnect MCP (in Claude Code)
/mcp
```

### Testing After Rebuild

```bash
# Verify MCP is running
erp__health_check()
# Should return: status=ok and registered ERPs

# List operations
erp__list_operations(erp="acumatica")

# Test knowledge query
erp__query_knowledge(erp="acumatica", query="your new operation name")
# Should find your newly added operation
```

---

## File Size Guidelines

**Target:** 200-350 lines per category file
**Maximum:** 500 lines (if exceeded, consider splitting category)

**Current sizes (as reference):**
- `operations/vendors.json` - 359 lines ✅
- `operations/payments.json` - ~300 lines ✅
- `operations/fields.json` - ~280 lines ✅

**Why size matters:**
- AI reads entire file when updating
- Smaller files = faster lookups
- Easier to maintain and review

**If file exceeds 500 lines:**
- Consider splitting by sub-category (e.g., `vendor-export.json` vs `vendor-import.json`)
- Or move complex examples to separate reference files

---

## Troubleshooting Integration

### Where to Document Errors

**Operation-specific errors:**
- Add `"troubleshooting"` section to operation entry in operations file
- Example: See `custom-field-operations.json` lines 163-233

**General errors affecting multiple operations:**
- Add to `error-catalog.json` in root
- Structure:
  ```json
  {
    "operationErrors": {
      "exportVendor": {
        "validation": [...],
        "businessLogic": [...]
      }
    }
  }
  ```

**Error patterns by category:**
- Use `diagnose_error` tool which reads `error-catalog.json`
- AI will suggest related validation rules from operation's flow

---

## Common Pitfalls for AI

### ❌ Mistake 1: Using Object Format
```json
"operations": {
  "exportVendor": {...}  ← WRONG
}
```
**Fix:** Use array format with objects inside

### ❌ Mistake 2: Forgetting to Update Count
**Fix:** Always increment/decrement categories.json count

### ❌ Mistake 3: Adding to Wrong Category
**Fix:** Use decision tree at top of this guide

### ❌ Mistake 4: Not Adding searchKeywords
**Fix:** Always include searchKeywords for fuzzy matching

### ❌ Mistake 5: Missing scanThese
**Fix:** Include scanThese for all operations with implementation

### ❌ Mistake 6: Not Reminding to Rebuild
**Fix:** Always remind user to rebuild after knowledge changes

### ❌ Mistake 7: Mixing WSL and Windows Paths
**Fix:** Use Windows paths in JSON (`C:\STAMPLI4\...`), WSL paths in bash commands (`/mnt/c/...`)

---

## Example: Adding New Operation

**Scenario:** User says "I implemented `exportBankAccount`, update MCP"

### Step 1: Determine Category
- Bank account operation → Check decision tree
- Accounts category exists → Use `operations/accounts.json`

### Step 2: Read Template
```bash
# Copy structure from _operation_template.json
```

### Step 3: Fill in Required Fields
```json
{
  "method": "exportBankAccount",
  "summary": "Creates or updates bank account in Acumatica with validation",
  "category": "accounts",
  "flow": "standard_export_flow",
  "searchKeywords": ["bank account", "export bank", "create bank account", "bank export"]
}
```

### Step 4: Add to operations/accounts.json
```json
{
  "category": "accounts",
  "operations": [
    { "method": "getAccountSearchList", ... },
    { "method": "getPayableAccountSearchList", ... },
    {
      "method": "exportBankAccount",
      "summary": "Creates or updates bank account in Acumatica with validation",
      "category": "accounts",
      ...
    }  ← Added here
  ]
}
```

### Step 5: Update categories.json
```json
{ "name": "accounts", "count": 7, "description": "..." }  ← Was 6, now 7
```

### Step 6: Remind User
```
✅ Added exportBankAccount to operations/accounts.json
✅ Updated categories.json count: accounts 6→7
⚠️  Rebuild required! Run:
    1. /mnt/c/Windows/System32/taskkill.exe /F /IM stampli-mcp-unified.exe
    2. dotnet build -c Release --nologo
    3. dotnet publish ... (full command)
    4. /mcp
```

---

## Schema Validation (Optional)

After adding operation, validate against `KNOWLEDGE_SCHEMA.json`:

```bash
# If you have schema validator installed
jsonschema -i operations/accounts.json KNOWLEDGE_SCHEMA.json
```

This checks:
- Required fields present
- Field types correct
- No unknown fields
- Valid category reference

---

## Questions? Decision Tree

```
❓ Which category file?
   → See "Category Decision Tree" section above

❓ What format (array vs object)?
   → ALWAYS use array format

❓ Which fields are required?
   → method, summary, category (minimum)
   → Add searchKeywords, flow, scanThese for completeness

❓ Do I update categories.json?
   → YES, always increment count when adding operation

❓ How do I know if it worked?
   → Rebuild, then test with list_operations and query_acumatica_knowledge

❓ Should I create new category?
   → Only if existing category would exceed 12-15 operations
   → Prefer existing category

❓ Where do I document errors?
   → Operation-specific: add troubleshooting section to operation
   → General errors: add to error-catalog.json
```

---

## Validation Checklist

Before telling user "Done!", verify:

- [ ] Operation added to correct category file
- [ ] Uses array format `"operations": [...]`
- [ ] Has required fields: method, summary, category
- [ ] Has searchKeywords for fuzzy matching
- [ ] Has scanThese if implementation exists
- [ ] categories.json count updated (+1)
- [ ] File size still under 500 lines
- [ ] Valid JSON (no syntax errors)
- [ ] Reminded user to rebuild with exact commands

---

## Summary: AI Quick Reference

```
1. Read user's operation details
2. Decision tree → determine category file
3. Use _operation_template.json as skeleton
4. Fill in required fields (method, summary, category)
5. Add searchKeywords (5-15 keywords)
6. Add scanThese (critical files to read)
7. Add to operations/{category}.json in ARRAY format
8. Update categories.json count (+1)
9. Validate JSON syntax
10. Remind user: REBUILD REQUIRED (provide exact commands)
```

**That's it!** Follow these steps and knowledge addition will be consistent, complete, and AI-friendly.
