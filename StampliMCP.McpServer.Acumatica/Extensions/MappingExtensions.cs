using StampliMCP.McpServer.Acumatica.Models;

namespace StampliMCP.McpServer.Acumatica.Extensions;

public static class MappingExtensions
{
    public static object ToToolResult(this Operation operation)
    {
        var result = new
        {
            operation = operation.Method,
            enumName = operation.EnumName,
            category = operation.Category,
            summary = operation.Summary,
            pattern = operation.Pattern,
            
            requiredFields = operation.RequiredFields,
            optionalFields = operation.OptionalFields,
            
            scanThese = operation.ScanThese.Select(s => new
            {
                file = s.File,
                lines = s.Lines,
                purpose = s.Purpose
            }),
            
            flowTrace = operation.FlowTrace?.Select(f => new
            {
                layer = f.Layer,
                file = f.File,
                lines = f.Lines,
                what = f.What
            }),
            
            helpers = operation.Helpers?.Select(h => new
            {
                @class = h.Class,
                location = new
                {
                    file = h.Location.File,
                    lines = h.Location.Lines,
                    purpose = h.Location.Purpose
                },
                purpose = h.Purpose,
                usage = h.Usage
            }),
            
            requestDtoLocation = operation.RequestDtoLocation is not null ? new
            {
                file = operation.RequestDtoLocation.File,
                lines = operation.RequestDtoLocation.Lines,
                purpose = operation.RequestDtoLocation.Purpose
            } : null,
            
            responseDtoLocation = operation.ResponseDtoLocation is not null ? new
            {
                file = operation.ResponseDtoLocation.File,
                lines = operation.ResponseDtoLocation.Lines,
                purpose = operation.ResponseDtoLocation.Purpose
            } : null,
            
            apiEndpoint = operation.ApiEndpoint,
            
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
            
            errorCatalogRef = operation.ErrorCatalogRef
        };
        
        return result;
    }

    public static object ToLightweightResult(this Operation operation) => new
    {
        method = operation.Method,
        summary = operation.Summary,
        pattern = operation.Pattern
    };

    public static object ToCategoryResult(this Category category) => new
    {
        name = category.Name,
        count = category.Count,
        description = category.Description
    };
}

