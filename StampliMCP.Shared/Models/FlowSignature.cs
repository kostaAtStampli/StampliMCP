using System.Collections.Generic;

namespace StampliMCP.Shared.Models;

public sealed class FlowSignature
{
    public string Name { get; set; } = string.Empty;
    public List<string> Actions { get; set; } = new();
    public List<string> Entities { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
}

public sealed class FlowSignatureCatalog
{
    public List<FlowSignature> Flows { get; set; } = new();
}
