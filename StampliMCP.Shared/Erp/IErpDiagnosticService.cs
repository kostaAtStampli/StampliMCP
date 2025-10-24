using StampliMCP.Shared.Models;

namespace StampliMCP.Shared.Erp;

public interface IErpDiagnosticService
{
    Task<ErrorDiagnostic> DiagnoseAsync(string errorMessage, CancellationToken ct);
}
