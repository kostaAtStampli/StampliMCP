using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Prompts;

[McpServerPromptType]
public sealed class ModernTddPrompt
{
    [McpServerPrompt(Name = "modern_tdd_conversation")]
    [Description("Interactive TDD conversation for Kotlin test + Java implementation")]
    public static ChatMessage[] CreateConversation(
        [Description("Feature to implement")]
        string feature)
    {
        return new[]
        {
            new ChatMessage(ChatRole.System,
                $@"You are a TDD expert guiding Kotlin test + Java implementation.
                
                REALITY CHECK:
                - Tests: Kotlin with LiveErpTestBase
                - Implementation: Java (for now)
                - Only exportVendor is in Kotlin
                - Harness: LiveErpTestBase + DSLs
                - Environment: ENV1 (63.32.187.185)
                
                WORKFLOW:
                1. Write Kotlin test (RED)
                2. Implement in Java (GREEN)
                3. Refactor if needed
                
                Your role: Guide step-by-step, ask for missing info, provide exact code."),
            
            new ChatMessage(ChatRole.User, $"I need to implement {feature}"),
            
            new ChatMessage(ChatRole.Assistant,
                $@"Let's implement {feature} using TDD! I'll guide you through:
                
                1️⃣ **Write Kotlin Test** (RED phase)
                2️⃣ **Implement in Java** (GREEN phase)
                3️⃣ **Refactor** (if needed)
                
                First, let me understand your requirement better:
                
                🔍 **Calling MCP tools to analyze...**
                [Tool: erp__query_knowledge with erp='acumatica', query='{feature}']
                [Tool: erp__recommend_flow with erp='acumatica', useCase='{feature}']
                
                Based on the analysis, I'll need some details:
                - Which entity? (vendor, invoice, payment, PO)
                - What operation? (export, import, update)
                - Any special validation rules?
                
                Once clarified, I'll generate:
                ✅ Complete Kotlin test with LiveErpTestBase
                ✅ Java implementation skeleton
                ✅ Validation rules to follow
                ✅ Run commands
                
                Let's start - which entity are we working with?")
        };
    }
}

