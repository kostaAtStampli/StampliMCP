using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Extensions;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class OperationTools
{
    [McpServerTool(Name = "get_operation", UseStructuredContent = true)]
    [Description("Get detailed information about a specific Acumatica operation including code pointers, required fields, and test examples")]
    public static async ValueTask<object> GetOperation(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("The operation method name (e.g., 'exportVendor', 'getVendors')")] string methodName,
        CancellationToken cancellationToken)
    {
        var operation = await knowledge.FindOperationAsync(methodName, cancellationToken);

        // Use modern pattern matching
        return operation switch
        {
            not null => operation.ToToolResult(),
            _ => new { error = $"Operation '{methodName}' not found" }
        };
    }

    [McpServerTool(Name = "list_operations", UseStructuredContent = true)]
    [Description("List all operations in a specific category or all categories")]
    public static async ValueTask<object> ListOperations(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("Category name (vendors, items, purchaseOrders, payments, accounts, fields, admin, retrieval, utility). Leave empty for all.")]
        string? category,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(category))
        {
            var categories = await knowledge.GetCategoriesAsync(cancellationToken);
            List<string> allOps = [];

            foreach (var cat in categories)
            {
                var ops = await knowledge.GetOperationsByCategoryAsync(cat.Name, cancellationToken);
                allOps.AddRange(ops.Select(o => o.Method));
            }

            return new { operations = allOps, total = allOps.Count };
        }

        var operations = await knowledge.GetOperationsByCategoryAsync(category, cancellationToken);
        return new
        {
            category,
            operations = operations.Select(o => o.ToLightweightResult()),
            count = operations.Count
        };
    }

    [McpServerTool(Name = "get_operation_flow", UseStructuredContent = true)]
    [Description("Get the end-to-end flow trace for an operation showing how it moves through service layers")]
    public static async ValueTask<object> GetOperationFlow(
        [FromKeyedServices("knowledge")] KnowledgeService knowledge,
        [Description("The operation method name")] string methodName,
        CancellationToken cancellationToken)
    {
        var operation = await knowledge.FindOperationAsync(methodName, cancellationToken);

        return operation switch
        {
            not null => new
            {
                operation = operation.Method,
                flowSteps = operation.ScanThese.Select(s => new
                {
                    s.File,
                    s.Lines,
                    s.Purpose
                })
            },
            _ => new { error = $"Operation '{methodName}' not found" }
        };
    }
}

