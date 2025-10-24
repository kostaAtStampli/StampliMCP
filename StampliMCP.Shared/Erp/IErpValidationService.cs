using StampliMCP.Shared.Models;

namespace StampliMCP.Shared.Erp;

public interface IErpValidationService
{
    Task<ValidationResult> ValidateAsync(string operation, string payload, CancellationToken ct);
}
