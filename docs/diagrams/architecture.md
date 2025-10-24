# Acumatica MCP Server Architecture

## System Flow

```mermaid
flowchart TD
    A[Claude Code / IDE] -->|stdio| B[MCP Server<br/>stampli-mcp-unified.exe]
    B -->|loads| C[Embedded Knowledge<br/>operations + flows]

    B --> D{MCP Tools}
    D --> E[erp__list_flows]
    D --> F[erp__get_flow_details]
    D --> G[erp__query_knowledge]
    D --> H[erp__validate_request]
    D --> I[erp__diagnose_error]

    C --> J[Operations<br/>operations/*.json]
    C --> K[Flows<br/>flows/*.json]
    C --> L[Error Catalog<br/>error-catalog.json]
    C --> M[Kotlin Patterns<br/>kotlin/*.md]

    style B fill:#4CAF50
    style C fill:#2196F3
    style D fill:#FF9800
```

## Component Architecture

```mermaid
graph LR
    subgraph "MCP Server"
        A[Program.cs<br/>MCP Host] --> B[Tools/<br/>erp__ tools]
        A --> C[Services/<br/>Business Logic]
        A --> D[Models/<br/>Data Structures]

        C --> E[KnowledgeService<br/>Load & Cache]
        C --> F[FlowService<br/>Flow Matching]
        C --> G[FuzzyMatchingService<br/>Search]

        E -->|embeds| H[Knowledge/<br/>embedded]
    end

    subgraph "Knowledge Files"
        H --> I[operations/<br/>11 categories]
        H --> J[flows/<br/>9 flows]
        H --> K[kotlin/<br/>7 docs]
        H --> L[*.json<br/>Config files]
    end

    style A fill:#4CAF50
    style E fill:#2196F3
    style H fill:#FF9800
```

## Data Flow: Operation Query

```mermaid
sequenceDiagram
    participant IDE as Claude Code
    participant MCP as MCP Server
    participant KS as KnowledgeService
    participant FS as FlowService

    IDE->>MCP: erp__query_knowledge(erp='acumatica', query='vendor')
    MCP->>KS: GetAllOperationsAsync()
    KS->>KS: Load operations/*.json from embedded resources
    KS-->>MCP: 6 vendor operations
    MCP->>FS: GetFlowForOperationAsync("exportVendor")
    FS-->>MCP: "vendor_export_flow"
    MCP-->>IDE: Lightweight JSON (~500 bytes)<br/>+ code pointers (file:lines)
    IDE->>IDE: Read pointed files for deep understanding
```

## Request/Response Pattern

```mermaid
flowchart LR
    A[User asks question] --> B[IDE queries MCP]
    B --> C{Which tool?}

    C -->|Operation details| D[query_knowledge]
    C -->|Flow anatomy| E[get_flow_details]
    C -->|Validation| F[validate_request]
    C -->|Error help| G[diagnose_error]

    D --> H[Returns code pointers]
    E --> H
    F --> H
    G --> H

    H --> I[IDE reads pointed files]
    I --> J[Deep understanding]
    J --> K[Generate accurate code]

    style A fill:#4CAF50
    style H fill:#FF9800
    style K fill:#2196F3
```

## Key Insights

1. **Code GPS, Not Document Dumper**
   - MCP returns lightweight metadata (~500 bytes)
   - Points to exact file locations (file:line_range)
   - IDE reads files for deep context
   - Result: ~10KB context vs 50KB+ dump

2. **Embedded Knowledge**
   - All 51 files compiled into exe
   - Single-file deployment (~108 MB)
   - No runtime file path issues
   - Trade-off: Rebuild required for knowledge changes

3. **Flow-Based Architecture**
   - 9 proven integration patterns
   - Operations reference flows
   - Flow anatomy + constants + validation rules
   - Reusable across operations

4. **Auto-Discovery**
   - Flows auto-discovered from embedded resources
   - No C# code changes to add flows
   - Categories require KnowledgeService.cs mapping
