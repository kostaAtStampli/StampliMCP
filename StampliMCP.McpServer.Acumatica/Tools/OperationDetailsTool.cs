using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class OperationDetailsTool
{
    [McpServerTool(Name = "get_operation_details")]
    [Description("Get surgical details for implementing a specific Acumatica operation in Kotlin using TDD. Returns exact validation rules, error messages, file pointers to legacy code, and golden pattern references.")]
    public static async Task<object> GetOperationDetails(
        [Description("Operation method name (e.g., 'exportVendor', 'getVendors', 'exportAPTransaction')")]
        string methodName,
        KnowledgeService knowledge,
        CancellationToken ct = default)
    {
        var operation = await knowledge.FindOperationAsync(methodName, ct);

        if (operation is null)
        {
            return new
            {
                error = $"Operation '{methodName}' not found. Use search_operations or get_categories to discover available operations.",
                suggestion = "Try searching for partial matches like 'vendor', 'bill', or 'payment'"
            };
        }

        // Return surgical details optimized for TDD implementation
        return new
        {
            method = operation.Method,
            enumName = operation.EnumName,
            summary = operation.Summary,
            category = operation.Category,

            // Validation rules - exact field requirements
            requiredFields = operation.RequiredFields.Select(f => new
            {
                name = f.Key,
                type = f.Value.Type,
                maxLength = f.Value.MaxLength,
                aliases = f.Value.Aliases,
                description = f.Value.Description
            }),

            optionalFields = operation.OptionalFields,

            // File pointers - NOT code snippets (token efficient)
            scanFiles = operation.ScanThese.Select(s => new
            {
                path = s.File,
                lines = s.Lines,
                purpose = s.Purpose,
                hint = $"Read lines {s.Lines} from C:\\STAMPLI4\\core\\{s.File}"
            }),

            // Golden patterns reference
            goldenPattern = new
            {
                file = "Knowledge/kotlin/GOLDEN_PATTERNS.md",
                section = $"Pattern for {operation.Method}",
                hint = "Copy-paste exact implementation patterns from this file"
            },

            // Test examples
            goldenTest = operation.GoldenTest is not null ? new
            {
                file = operation.GoldenTest.File,
                lines = operation.GoldenTest.Lines,
                keyTests = operation.GoldenTest.KeyTests?.Select(t => new
                {
                    method = t.Method,
                    lines = t.Lines,
                    purpose = t.Purpose
                })
            } : null,

            // Error catalog reference
            errorPatterns = new
            {
                file = "Knowledge/kotlin/error-patterns-kotlin.json",
                hint = "Use EXACT error messages from this file for validation errors"
            },

            // Flow trace (if available)
            flowTrace = operation.FlowTrace?.Select(f => new
            {
                layer = f.Layer,
                file = f.File,
                lines = f.Lines,
                what = f.What
            }),

            // API endpoint details
            apiEndpoint = operation.ApiEndpoint is not null ? new
            {
                entity = operation.ApiEndpoint.Entity,
                method = operation.ApiEndpoint.Method,
                urlPattern = operation.ApiEndpoint.UrlPattern,
                pagination = operation.ApiEndpoint.Pagination,
                requestExample = operation.ApiEndpoint.RequestBodyExample,
                responseExample = operation.ApiEndpoint.ResponseExample
            } : null,

            // DTO locations
            requestDto = operation.RequestDtoLocation is not null ? new
            {
                file = operation.RequestDtoLocation.File,
                lines = operation.RequestDtoLocation.Lines,
                purpose = operation.RequestDtoLocation.Purpose
            } : null,

            responseDto = operation.ResponseDtoLocation is not null ? new
            {
                file = operation.ResponseDtoLocation.File,
                lines = operation.ResponseDtoLocation.Lines,
                purpose = operation.ResponseDtoLocation.Purpose
            } : null,

            // Helper classes
            helpers = operation.Helpers?.Select(h => new
            {
                className = h.Class,
                file = h.Location.File,
                lines = h.Location.Lines,
                purpose = h.Purpose,
                usage = h.Usage
            }),

            // TDD workflow hint
            tddWorkflow = new
            {
                file = "Knowledge/kotlin/03_AI_TDD_WORKFLOW.xml",
                reminder = "Follow 7-step TDD workflow: Query → Scan → Test(FAIL) → Implement → Test(PASS)"
            }
        };
    }
}
