using System.ComponentModel;
using ModelContextProtocol.Server;
using StampliMCP.McpServer.Acumatica.Services;

namespace StampliMCP.McpServer.Acumatica.Tools;

// Internal helper - no longer exposed as MCP tool
public static class ErrorTools
{
    // Called internally by KotlinTddWorkflowTool
    internal static async Task<object> GetErrors(
        string? operation,
        KnowledgeService knowledge,
        CancellationToken cancellationToken)
    {
        var catalog = await knowledge.GetErrorCatalogAsync(cancellationToken);

        if (string.IsNullOrEmpty(operation))
        {
            // Return general errors only
            return new
            {
                authenticationErrors = catalog.AuthenticationErrors,
                apiErrors = catalog.ApiErrors
            };
        }

        // Return operation-specific errors
        if (catalog.OperationErrors?.TryGetValue(operation, out var opErrors) == true)
        {
            return new
            {
                operation,
                validation = opErrors.Validation,
                businessLogic = opErrors.BusinessLogic,
                authenticationErrors = catalog.AuthenticationErrors,
                apiErrors = catalog.ApiErrors
            };
        }

        return new
        {
            operation,
            message = "No specific errors catalogued for this operation",
            authenticationErrors = catalog.AuthenticationErrors,
            apiErrors = catalog.ApiErrors
        };
    }
}

file sealed record ErrorCatalog(
    List<AuthError> AuthenticationErrors,
    Dictionary<string, OperationErrors> OperationErrors,
    List<ApiError> ApiErrors);

file sealed record AuthError(string Type, string Message, ErrorLocation Location, string TestAssertion);

file sealed record OperationErrors(List<ValidationError> Validation, List<BusinessError> BusinessLogic);

file sealed record ValidationError(string Field, string Condition, string Message, ErrorLocation Location, string TestAssertion);

file sealed record BusinessError(string Type, string Message, ErrorLocation Location, TestExample TestExample, string TestAssertion, string? Description = null);

file sealed record ErrorLocation(string File, string? Lines = null);

file sealed record TestExample(string File, string Lines, string Method);

file sealed record ApiError(int Code, string Message, string Handling, ErrorLocation? Location = null);

