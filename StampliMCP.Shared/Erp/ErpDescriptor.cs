namespace StampliMCP.Shared.Erp;

public sealed record ErpDescriptor(
    string Key,
    IReadOnlyList<string> Aliases,
    ErpCapability Capabilities,
    string? Version = null,
    string? Description = null)
{
    public bool Supports(ErpCapability capability) => (Capabilities & capability) == capability;
}
