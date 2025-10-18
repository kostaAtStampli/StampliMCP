using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace StampliMCP.McpServer.Acumatica.Models;

// Note: ResourceLinkBlock is now provided by SDK in ModelContextProtocol.Protocol

// ================== Knowledge Query Results ==================

[Description("Result from knowledge base query")]
public class KnowledgeQueryResult
{
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

[Description("Summary of an Acumatica operation")]
public class OperationSummary
{
    [Description("Operation method name (e.g., exportVendor, getPayments)")]
    public string Method { get; set; } = string.Empty;

    [Description("Brief description of what the operation does")]
    public string Summary { get; set; } = string.Empty;

    [Description("Category (vendors, payments, purchaseOrders, etc.)")]
    public string Category { get; set; } = string.Empty;

    [Description("Associated flow pattern")]
    public string? Flow { get; set; }
}

[Description("Summary of an integration flow")]
public class FlowSummary
{
    [Description("Flow name (e.g., VENDOR_EXPORT_FLOW, PAYMENT_FLOW)")]
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
    [Description("High-level flow description (e.g., Validate → Map → Create → Extract → Return)")]
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
}

// ================== Error Diagnostic ==================

[Description("Error diagnostic result with causes and solutions")]
public class ErrorDiagnostic
{
    [Description("Original error message")]
    public string ErrorMessage { get; set; } = string.Empty;

    [Description("Error category")]
    public string ErrorCategory { get; set; } = string.Empty; // Validation, BusinessLogic, Authentication, Network, Unknown

    [Description("Possible causes of the error")]
    public List<string> PossibleCauses { get; set; } = new();

    [Description("Solutions to fix the error")]
    public List<ErrorSolution> Solutions { get; set; } = new();

    [Description("Related flow validation rules")]
    public List<string> RelatedFlowRules { get; set; } = new();

    [Description("Tips to prevent this error in the future")]
    public List<string> PreventionTips { get; set; } = new();

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

// ================== TDD Workflow Result ==================

[Description("TDD workflow result with structured steps")]
public class TddWorkflowResult
{
    [Description("User's feature request")]
    public string UserRequest { get; set; } = string.Empty;

    [Description("Selected flow for implementation")]
    public string SelectedFlow { get; set; } = string.Empty;

    [Description("Flow description")]
    public string FlowDescription { get; set; } = string.Empty;

    [Description("Kotlin patterns from golden reference")]
    public object? KotlinPatterns { get; set; }

    [Description("Structured TDD steps")]
    public List<TddStep> TddSteps { get; set; } = new();

    [Description("Validation rules to implement")]
    public List<string> ValidationRules { get; set; } = new();

    [Description("Constants to use")]
    public Dictionary<string, object> Constants { get; set; } = new();

    [Description("Mandatory files to scan")]
    public List<FileReference> MandatoryFileScanning { get; set; } = new();

    [Description("Next actions")]
    public List<ResourceLinkBlock> NextActions { get; set; } = new();
}

[Description("TDD step")]
public class TddStep
{
    [Description("Step number")]
    public int StepNumber { get; set; }

    [Description("Step type: RED (test), GREEN (implementation), or REFACTOR")]
    public string Type { get; set; } = string.Empty;

    [Description("Step description")]
    public string Description { get; set; } = string.Empty;

    [Description("Acceptance criteria")]
    public List<string> AcceptanceCriteria { get; set; } = new();
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
