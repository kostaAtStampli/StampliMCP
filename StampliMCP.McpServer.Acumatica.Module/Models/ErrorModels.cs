namespace StampliMCP.McpServer.Acumatica.Models;

public sealed record ErrorCatalog
{
    public List<ErrorDetail>? AuthenticationErrors { get; init; }
    public Dictionary<string, OperationErrorSet>? OperationErrors { get; init; }
    public List<ApiError>? ApiErrors { get; init; }
}

public sealed record OperationErrorSet
{
    public List<ErrorDetail>? Validation { get; init; }
    public List<ErrorDetail>? BusinessLogic { get; init; }
}

public sealed record ErrorDetail
{
    public string? Field { get; init; }
    public string? Condition { get; init; }
    public string? Type { get; init; }
    public required string Message { get; init; }
    public CodeLocation? Location { get; init; }
    public string? TestAssertion { get; init; }
}

public sealed record ApiError
{
    public int Code { get; init; }
    public required string Message { get; init; }
    public string? Handling { get; init; }
    public CodeLocation? Location { get; init; }
}

public sealed record CodeLocation
{
    public required string File { get; init; }
    public string? Lines { get; init; }
}
