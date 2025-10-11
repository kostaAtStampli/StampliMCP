namespace StampliMCP.McpServer.Acumatica.Models;

public sealed record Operation
{
    required public string Method { get; init; }
    required public string EnumName { get; init; }
    required public string Summary { get; init; }
    required public string Category { get; init; }
    public Dictionary<string, FieldInfo> RequiredFields { get; init; } = [];
    public Dictionary<string, object>? OptionalFields { get; init; }
    public List<CodePointer> ScanThese { get; init; } = [];
    public GoldenTest? GoldenTest { get; init; }
    public List<FlowStep>? FlowTrace { get; init; }
    public List<HelperClass>? Helpers { get; init; }
    public DtoLocation? RequestDtoLocation { get; init; }
    public DtoLocation? ResponseDtoLocation { get; init; }
    public ApiEndpoint? ApiEndpoint { get; init; }
    public string? ErrorCatalogRef { get; init; }
    public string? Pattern { get; init; }
}

public sealed record FieldInfo(string Type, int? MaxLength = null, string[]? Aliases = null, string? Description = null);

public sealed record CodePointer(string File, string Lines, string? Purpose = null);

public sealed record GoldenTest(string File, string Lines, List<TestMethod>? KeyTests = null);

public sealed record TestMethod(string Method, string Lines, string Purpose);

public sealed record FlowStep(string Layer, string File, string Lines, string What);

public sealed record HelperClass(string Class, DtoLocation Location, string Purpose, string? Usage = null);

public sealed record DtoLocation(string File, string Lines, string Purpose);

public sealed record ApiEndpoint(
    string? Entity = null, 
    string? Method = null, 
    string? UrlPattern = null, 
    bool? Pagination = null,
    bool? DeltaSupport = null,
    string? RequestBodyExample = null,
    string? ResponseExample = null,
    string? ErrorResponseExample = null,
    string[]? Entities = null,
    string? Note = null);

public sealed record Category(string Name, int Count, string Description);

public sealed record EnumMapping(string Name, DtoLocation Location, Dictionary<string, string> Mappings);

