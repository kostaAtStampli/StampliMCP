using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace StampliMCP.McpServer.Acumatica.Tools;

[McpServerToolType]
public static class ErrorTools
{
    [McpServerTool(Name = "get_errors")]
    [Description("Get error catalog for a specific operation or all authentication/API errors")]
    public static async Task<object> GetErrors(
        [Description("Operation method name (e.g., 'exportVendor'). Leave empty for general errors only.")] 
        string? operation,
        CancellationToken cancellationToken)
    {
        var errorCatalogPath = Path.Combine(AppContext.BaseDirectory, "Knowledge", "error-catalog.json");
        var json = await File.ReadAllTextAsync(errorCatalogPath, cancellationToken);
        var catalog = JsonSerializer.Deserialize<ErrorCatalog>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        if (catalog is null)
            return new { error = "Error catalog not found" };

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

