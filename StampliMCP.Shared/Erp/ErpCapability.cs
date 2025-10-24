namespace StampliMCP.Shared.Erp;

[System.Flags]
public enum ErpCapability
{
    None = 0,
    Knowledge = 1 << 0,
    Flows = 1 << 1,
    Validation = 1 << 2,
    Diagnostics = 1 << 3,
    Recommendation = 1 << 4,
}
