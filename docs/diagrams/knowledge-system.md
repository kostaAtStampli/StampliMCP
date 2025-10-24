# Knowledge System Structure

## 7-Layer Knowledge Architecture

```mermaid
graph TD
    subgraph "Layer 1: Operations (Core Integration)"
        A1[categories.json<br/>categories registry]
        A2[operations/*.json<br/>ARRAY format]
        A3[custom-field-operations.json<br/>$adHocSchema]
    end

    subgraph "Layer 2: Flows (Integration Patterns)"
        B1[flows/*.json<br/>proven patterns]
        B2[vendor_export_flow<br/>payment_flow<br/>standard_import_flow<br/>...]
    end

    subgraph "Layer 3: Errors (Debugging)"
        C1[error-catalog.json<br/>Auth/Validation/Business errors]
    end

    subgraph "Layer 4: Kotlin Migration (Modern)"
        D1[kotlin/*.md<br/>TDD patterns]
        D2[kotlin-golden-reference.json<br/>exportVendor example]
        D3[modern-infrastructure.json<br/>Test harness]
    end

    subgraph "Layer 5: Strategy (Architecture)"
        E1[integration-strategy.json<br/>Migration phases]
        E2[base-classes.json<br/>Request/Response DTOs]
        E3[custom-field-patterns.json<br/>DAC mappings]
    end

    subgraph "Layer 6: Docs (AI Guides)"
        F1[KNOWLEDGE_CONTRIBUTING.md<br/>Decision trees]
        F2[_operation_template.json<br/>Skeleton]
        F3[KNOWLEDGE_SCHEMA.json<br/>Validation]
    end

    subgraph "Layer 7: Config (Test & Enums)"
        G1[test-config.json]
        G2[enums.json]
        G3[payment-flows.json]
    end

    style A1 fill:#4CAF50
    style B1 fill:#2196F3
    style C1 fill:#f44336
    style D1 fill:#FF9800
    style E1 fill:#9C27B0
    style F1 fill:#00BCD4
    style G1 fill:#795548
```

## Knowledge File Structure

```
Knowledge/
├── Layer 1: Operations (What to do)
│   ├── categories.json                     ← Registry of 11 categories
│   ├── operations/                         ← Rich operation knowledge
│   │   ├── vendors.json (4 ops)
│   │   ├── items.json (2 ops)
│   │   ├── purchaseOrders.json (6 ops)
│   │   ├── payments.json (10 ops)
│   │   ├── accounts.json (6 ops)
│   │   ├── fields.json (10 ops)
│   │   ├── admin.json (9 ops)
│   │   ├── retrieval.json (1 op)
│   │   ├── utility.json (2 ops)
│   │   └── other.json (2 ops)
│   └── custom-field-operations.json (5 ops)
│
├── Layer 2: Flows (How to do it)
│   └── flows/
│       ├── vendor_export_flow.json
│       ├── payment_flow.json
│       ├── standard_import_flow.json
│       ├── po_matching_flow.json
│       ├── po_matching_full_import_flow.json
│       ├── export_invoice_flow.json
│       ├── export_po_flow.json
│       ├── m2m_import_flow.json
│       └── api_action_flow.json
│
├── Layer 3: Errors (What went wrong)
│   └── error-catalog.json
│
├── Layer 4: Kotlin (Modern patterns)
│   ├── kotlin/
│   │   ├── GOLDEN_PATTERNS.md (433 lines)
│   │   ├── KOTLIN_ARCHITECTURE.md (171 lines)
│   │   ├── TDD_WORKFLOW.md (318 lines)
│   │   ├── ACUMATICA_COMPLETE_ANALYSIS.md (1341 lines)
│   │   ├── kotlin-integration.json
│   │   ├── method-signatures.json
│   │   ├── error-patterns-kotlin.json
│   │   └── test-config-kotlin.json
│   ├── kotlin-golden-reference.json
│   ├── kotlin-reality.json
│   └── modern-infrastructure.json
│
├── Layer 5: Strategy (Architecture)
│   ├── integration-strategy.json
│   ├── base-classes.json
│   ├── custom-field-patterns.json
│   └── method-signatures.json
│
├── Layer 6: Docs (How to add knowledge)
│   ├── KNOWLEDGE_CONTRIBUTING.md (697 lines)
│   ├── _operation_template.json
│   └── KNOWLEDGE_SCHEMA.json
│
└── Layer 7: Config (Test & Enum data)
    ├── test-config.json
    ├── enums.json
    ├── payment-flows.json
    └── reflection-mechanism.json
```

## Adding Knowledge: Decision Tree

```mermaid
flowchart TD
    A[I want to add...] --> B{What type?}

    B -->|Operation| C{Existing category?}
    C -->|Yes| D[Add to operations/category.json<br/>Increment categories.json count]
    C -->|No| E[Create new category<br/>⚠️ Requires C# mapping]

    B -->|Flow| F[Create flows/flow_name.json<br/>✅ Auto-discovered, no C# needed!]

    B -->|Error Pattern| G[Update error-catalog.json<br/>Under operation name]

    B -->|Kotlin Pattern| H[Update kotlin/*.md or<br/>kotlin-golden-reference.json]

    D --> I[Rebuild MCP Server]
    E --> J[Update KnowledgeService.cs mapping<br/>Then rebuild]
    F --> I
    G --> I
    H --> I

    I --> K[Test with erp__query_knowledge]
    J --> K
    K --> L{Found?}
    L -->|Yes| M[Done! ✅]
    L -->|No| N[Debug: Check file format,<br/>rebuild, reconnect]

    style D fill:#4CAF50
    style F fill:#2196F3
    style E fill:#FF9800
    style J fill:#f44336
```

## Knowledge Statistics

| Layer | Files | Lines | Purpose |
|-------|-------|-------|---------|
| **Operations** | 12 | ~3000 | Individual methods |
| **Flows** | 9 | ~900 | Reusable patterns |
| **Errors** | 1 | ~400 | Known errors/fixes |
| **Kotlin** | 10 | ~2500 | Modern patterns |
| **Strategy** | 4 | ~600 | Architecture |
| **Docs** | 3 | ~750 | AI guides |
| **Config** | 4 | ~200 | Test/enum data |
| **TOTAL** | 43 | ~9350 | Complete knowledge |

## Key Architectural Insights

### 1. Categories vs Flows

```mermaid
graph LR
    subgraph "Categories (Organizational)"
        A[11 categories] -->|groups| B[54 operations]
        A -->|requires| C[C# mapping]
    end

    subgraph "Flows (Patterns)"
        D[9 flows] -->|referenced by| E[multiple operations]
        D -->|auto-discovered| F[No C# changes!]
    end

    B -.->|references| D

    style A fill:#FF9800
    style D fill:#4CAF50
```

### 2. Embedded vs External

```mermaid
flowchart LR
    A[Knowledge/*.json] -->|compiled into| B[stampli-mcp-unified.exe]
    B -->|no runtime file access| C[Single-file deployment]
    C -->|trade-off| D[Rebuild required for changes]

    E[Alternative: External files] -->|pros| F[Hot-reload]
    E -->|cons| G[File path issues<br/>Deployment complexity]

    style B fill:#4CAF50
    style D fill:#FF9800
```

### 3. Knowledge Growth Pattern

```mermaid
gitGraph
    commit id: "Initial: 5 operations"
    commit id: "Add vendor operations (4)"
    commit id: "Add payment operations (10)"
    branch flows
    commit id: "Add vendor_export_flow"
    commit id: "Add payment_flow"
    checkout main
    merge flows
    commit id: "Add error catalog"
    commit id: "Add Kotlin patterns"
    commit id: "Current: 54 operations, 9 flows"
```

## How AI Adds Knowledge

```mermaid
sequenceDiagram
    participant AI as AI Agent
    participant Guide as KNOWLEDGE_CONTRIBUTING.md
    participant Template as _operation_template.json
    participant File as operations/category.json
    participant Cat as categories.json
    participant Build as MCP Server

    AI->>Guide: Read decision tree
    Guide-->>AI: Category: "payments"

    AI->>Template: Copy skeleton
    Template-->>AI: Required fields + structure

    AI->>File: Add operation in ARRAY format
    AI->>Cat: Increment count: payments 9→10

    AI->>Build: Rebuild server
    Build-->>AI: Knowledge embedded ✅

    AI->>Build: Test erp__query_knowledge
    Build-->>AI: Operation found! ✅
```

## Summary

**Knowledge System = 7 Layers of Intelligence**

1. **Operations** → What operations exist
2. **Flows** → How to implement them
3. **Errors** → What can go wrong
4. **Kotlin** → Modern implementation patterns
5. **Strategy** → Architecture decisions
6. **Docs** → How to add knowledge
7. **Config** → Test/enum metadata

**Result**: AI-friendly, searchable, maintainable knowledge base powering intelligent code generation!
