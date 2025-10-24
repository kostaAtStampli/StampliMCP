using System.ComponentModel;
using ModelContextProtocol.Protocol;

namespace StampliMCP.Shared.Models;

// ================== Knowledge Addition Result ==================

[Description("Result from automated knowledge addition from PR learnings")]
public sealed class KnowledgeAdditionResult
{
    [Description("Human-readable summary of the operation")]
    public string? Summary { get; set; }

    [Description("Decision verdict: ADD, SKIP, DUPLICATE, or BACKLOG")]
    public string Verdict { get; set; } = string.Empty;

    [Description("Reason for the verdict")]
    public string Reason { get; set; } = string.Empty;

    [Description("Operations that this duplicates (if verdict is DUPLICATE)")]
    public List<string> DuplicateOf { get; set; } = new();

    [Description("Category where operation was added (if verdict is ADD)")]
    public string? Category { get; set; }

    [Description("Files that were modified (if verdict is ADD)")]
    public List<string> FilesModified { get; set; } = new();

    [Description("Full operation JSON that was added (if verdict is ADD)")]
    public object? OperationAdded { get; set; }

    [Description("Rebuild status: success, failed, or skipped")]
    public string? RebuildStatus { get; set; }

    [Description("Suggestion for user action")]
    public string? Suggestion { get; set; }

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}
