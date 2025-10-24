using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Protocol;

namespace StampliMCP.Shared.Models;

// Note: ResourceLinkBlock is provided by SDK in ModelContextProtocol.Protocol

// ================== Knowledge Query Results ==================

[Description("Result from knowledge base query")]
public class KnowledgeQueryResult
{
    [Description("Human-readable summary of results")]
    public string? Summary { get; set; }

    [Description("Operations matching the query")]
    public List<OperationSummary> MatchedOperations { get; set; } = new();

    [Description("Related integration flow patterns")]
    public List<FlowSummary> RelevantFlows { get; set; } = new();

    [Description("Constants from flows (e.g., pagination limits, max lengths)")]
    public Dictionary<string, ConstantInfo> Constants { get; set; } = new();

    [Description("Code snippets from flow implementations")]
    public List<CodeSnippet> CodeExamples { get; set; } = new();

    [Description("Validation rules from flows")]
    public List<string> ValidationRules { get; set; } = new();

    [Description("Next actions - resource links to related tools")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("Summary of an operation")]
public class OperationSummary
{
    [Description("Operation method name")]
    public string Method { get; set; } = string.Empty;

    [Description("Brief description of what the operation does")]
    public string Summary { get; set; } = string.Empty;

    [Description("Category")]
    public string Category { get; set; } = string.Empty;

    [Description("Associated flow pattern")]
    public string? Flow { get; set; }
}

[Description("Summary of an integration flow")]
public class FlowSummary
{
    [Description("Flow name")]
    public string Name { get; set; } = string.Empty;

    [Description("What this flow does")]
    public string Description { get; set; } = string.Empty;

    [Description("Operations that use this flow")]
    public List<string> UsedByOperations { get; set; } = new();
}

// ================== Flow Details ==================

[Description("Complete flow details with anatomy, constants, and rules")]
public class FlowDetail
{
    [Description("Human-readable summary of flow details")]
    public string? Summary { get; set; }

    [Description("Flow name")]
    public string Name { get; set; } = string.Empty;

    [Description("Flow description")]
    public string Description { get; set; } = string.Empty;

    [Description("Flow anatomy - step-by-step structure")]
    public FlowAnatomy Anatomy { get; set; } = new();

    [Description("Constants used in this flow")]
    public Dictionary<string, ConstantInfo> Constants { get; set; } = new();

    [Description("Validation rules for this flow")]
    public List<string> ValidationRules { get; set; } = new();

    [Description("Code snippets from implementation")]
    public Dictionary<string, string> CodeSnippets { get; set; } = new();

    [Description("Critical files to review for implementation")]
    public List<FileReference> CriticalFiles { get; set; } = new();

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("Flow anatomy - structural information")]
public class FlowAnatomy
{
    [Description("High-level flow description")]
    public string Flow { get; set; } = string.Empty;

    [Description("Validation logic description")]
    public string? Validation { get; set; }

    [Description("Mapping/transformation logic")]
    public string? Mapping { get; set; }

    [Description("Additional flow-specific details")]
    public Dictionary<string, string> AdditionalInfo { get; set; } = new();
}

// ================== Flow Recommendation ==================

[Description("AI-powered flow recommendation result")]
public class FlowRecommendation
{
    [Description("Human-readable summary of recommendation")]
    public string? Summary { get; set; }

    [Description("Recommended flow name")]
    public string FlowName { get; set; } = string.Empty;

    [Description("Confidence score (0.0 to 1.0)")]
    public double Confidence { get; set; }

    [Description("Why this flow was recommended")]
    public string Reasoning { get; set; } = string.Empty;

    [Description("Complete flow details")]
    public FlowDetail Details { get; set; } = new();

    [Description("Alternative flows that might also work")]
    public List<AlternativeFlow> AlternativeFlows { get; set; } = new();

    [Description("Score breakdown (overall/action/entity/keywords)")]
    public Dictionary<string, double> Scores { get; set; } = new();

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("Alternative flow option")]
public class AlternativeFlow
{
    [Description("Flow name")]
    public string Name { get; set; } = string.Empty;

    [Description("Confidence score")]
    public double Confidence { get; set; }

    [Description("Why this could also work")]
    public string Reason { get; set; } = string.Empty;
}

// ================== Validation Result ==================

[Description("Request validation result")]
public class ValidationResult
{
    [Description("Human-readable summary of validation outcome")]
    public string? Summary { get; set; }

    [Description("Whether the request is valid")]
    public bool IsValid { get; set; }

    [Description("Operation being validated")]
    public string Operation { get; set; } = string.Empty;

    [Description("Flow used for validation")]
    public string Flow { get; set; } = string.Empty;

    [Description("Validation errors found")]
    public List<ValidationError> Errors { get; set; } = new();

    [Description("Non-blocking warnings")]
    public List<string> Warnings { get; set; } = new();

    [Description("Validation rules that were applied")]
    public List<string> AppliedRules { get; set; } = new();

    [Description("Suggestions to fix errors")]
    public List<string> Suggestions { get; set; } = new();

    [Description("Suggested payload with placeholders (when auto-fix is accepted)")]
    public string? SuggestedPayload { get; set; }

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("Validation error details")]
public class ValidationError
{
    [Description("Field name that failed validation")]
    public string Field { get; set; } = string.Empty;

    [Description("Validation rule that was violated")]
    public string Rule { get; set; } = string.Empty;

    [Description("Error message")]
    public string Message { get; set; } = string.Empty;

    [Description("Current value that failed")]
    public string? CurrentValue { get; set; }

    [Description("Expected value or format")]
    public string? Expected { get; set; }

    [Description("Origin of the rule (flow, constant, or file ref)")]
    public string? RuleSource { get; set; }
}

// ================== Error Diagnostic ==================

[Description("Error diagnostic result with causes and solutions")]
public class ErrorDiagnostic
{
    [Description("Human-readable summary of diagnostic")]
    public string? Summary { get; set; }

    [Description("Original error message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [Description("Error category")]
    public string ErrorCategory { get; set; } = string.Empty;

    [Description("Possible causes of the error")]
    public List<string> PossibleCauses { get; set; } = new();

    [Description("Solutions to fix the error")]
    public List<ErrorSolution> Solutions { get; set; } = new();

    [Description("Related flow validation rules")]
    public List<string> RelatedFlowRules { get; set; } = new();

    [Description("Tips to prevent this error in the future")]
    public List<string> PreventionTips { get; set; } = new();

    [Description("User-provided context captured during elicitation")]
    public Dictionary<string, string>? AdditionalContext { get; set; }

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("Error solution with code example")]
public class ErrorSolution
{
    [Description("Solution description")]
    public string Description { get; set; } = string.Empty;

    [Description("Code example showing the fix")]
    public string? CodeExample { get; set; }

    [Description("Reference to flow where this solution is documented")]
    public string? FlowReference { get; set; }
}

// ================== Supporting Types ==================

[Description("Constant information from flow")]
public class ConstantInfo
{
    [Description("Constant name")]
    public string Name { get; set; } = string.Empty;

    [Description("Constant value")]
    public string Value { get; set; } = string.Empty;

    [Description("File where constant is defined")]
    public string? File { get; set; }

    [Description("Line number where defined")]
    public int? Line { get; set; }

    [Description("Purpose/usage of this constant")]
    public string? Purpose { get; set; }
}

[Description("Code snippet from flow")]
public class CodeSnippet
{
    [Description("Snippet name/title")]
    public string Name { get; set; } = string.Empty;

    [Description("Code content")]
    public string Code { get; set; } = string.Empty;

    [Description("Language (kotlin, java, json, etc.)")]
    public string Language { get; set; } = string.Empty;

    [Description("Explanation of what this code does")]
    public string? Explanation { get; set; }
}

[Description("File reference with location info")]
public class FileReference
{
    [Description("File path")]
    public string File { get; set; } = string.Empty;

    [Description("Line range (e.g., '102-117')")]
    public string? Lines { get; set; }

    [Description("Purpose of this file")]
    public string? Purpose { get; set; }

    [Description("Key patterns to find in this file")]
    public List<string> KeyPatterns { get; set; } = new();
}
