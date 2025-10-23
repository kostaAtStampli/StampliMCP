using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StampliMCP.McpServer.Acumatica.Services;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using StampliMCP.Shared.Services;

namespace StampliMCP.McpServer.Acumatica.Module.Services.Validation;

public sealed class AcumaticaValidationService : IErpValidationService
{
    private readonly ILogger<AcumaticaValidationService> _logger;
    private readonly AcumaticaFlowService _flowService;
    private readonly AcumaticaKnowledgeService _knowledge;
    private readonly FuzzyMatchingService _fuzzy;

    public AcumaticaValidationService(
        ILogger<AcumaticaValidationService> logger,
        AcumaticaFlowService flowService,
        AcumaticaKnowledgeService knowledge,
        FuzzyMatchingService fuzzy)
    {
        _logger = logger;
        _flowService = flowService;
        _knowledge = knowledge;
        _fuzzy = fuzzy;
    }

    public async Task<ValidationResult> ValidateAsync(string operation, string payload, CancellationToken ct)
    {
        var flowName = await _flowService.GetFlowForOperationAsync(operation, ct)
                       ?? await FindFlowForOperationFallbackAsync(operation, ct);

        if (string.IsNullOrEmpty(flowName))
        {
            var (suggested, confidence) = await FindSimilarOperationAsync(operation, ct);

            var message = $"Operation '{operation}' not found";
            if (!string.IsNullOrEmpty(suggested))
            {
                message += $". Did you mean '{suggested}'? ({confidence:P0} match)";
            }

            return new ValidationResult
            {
                IsValid = false,
                Operation = operation,
                Flow = "UNKNOWN",
                Errors =
                [
                    new ValidationError
                    {
                        Field = "operation",
                        Rule = "operation_exists",
                        Message = message,
                        Expected = suggested ?? "Valid operation name"
                    }
                ],
                Suggestions = string.IsNullOrEmpty(suggested)
                    ? new List<string>()
                    : new List<string> { $"Try: {suggested}" }
            };
        }

        JsonDocument requestDoc;
        try
        {
            requestDoc = JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                Operation = operation,
                Flow = flowName,
                Errors =
                [
                    new ValidationError
                    {
                        Field = "requestPayload",
                        Rule = "valid_json",
                        Message = $"Invalid JSON: {ex.Message}",
                        Expected = "Valid JSON object"
                    }
                ]
            };
        }

        var errors = new List<ValidationError>();
        var warnings = new List<string>();
        var appliedRules = new List<string>();

        ApplyFallbackRequiredFields(requestDoc.RootElement, operation, errors, appliedRules);

        var fieldConstraints = new Dictionary<string, FieldConstraint>(StringComparer.OrdinalIgnoreCase);
        var generalRules = new List<string>();

        var flowDoc = await _flowService.GetFlowAsync(flowName, ct);
        if (flowDoc is not null)
        {
            ExtractFieldConstraints(flowDoc.RootElement, fieldConstraints, generalRules);
        }

        ApplyFieldConstraints(fieldConstraints, requestDoc.RootElement, errors, appliedRules);

        foreach (var rule in generalRules.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var (valid, error, warning) = ApplyGeneralRule(rule, requestDoc.RootElement);
            appliedRules.Add(rule);

            if (!valid && error is not null)
            {
                errors.Add(error);
            }

            if (warning is not null)
            {
                warnings.Add(warning);
            }
        }

        errors = errors
            .GroupBy(e => $"{e.Field}|{e.Rule}|{e.Message}")
            .Select(g => g.First())
            .ToList();

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Operation = operation,
            Flow = flowName,
            Errors = errors,
            Warnings = warnings,
            AppliedRules = appliedRules,
            Suggestions = errors.Select(e => $"Fix {e.Field}: {e.Expected}").ToList()
        };
    }

    private async Task<string?> FindFlowForOperationFallbackAsync(string operation, CancellationToken ct)
    {
        var operations = await _knowledge.GetAllOperationsAsync(ct);
        var match = operations.FirstOrDefault(o => o.Method.Equals(operation, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return null;
        }

        return await _flowService.GetFlowForOperationAsync(match.Method, ct);
    }

    private async Task<(string? Operation, double Confidence)> FindSimilarOperationAsync(string operation, CancellationToken ct)
    {
        var operations = await _knowledge.GetAllOperationsAsync(ct);
        var names = operations.Select(o => o.Method).ToList();
        var match = _fuzzy.FindBestMatch(operation, names, _fuzzy.GetThreshold("operation"));
        return match is null ? (null, 0d) : (match.Pattern, match.Confidence);
    }

    private (bool IsValid, ValidationError? Error, string? Warning) ApplyGeneralRule(string rule, JsonElement request)
    {
        var normalized = rule.ToLowerInvariant();

        if (normalized is "max_pagination_2000")
        {
            if (TryGetPropertyIgnoreCase(request, "pageSize", out var pageSizeEl) && pageSizeEl.TryGetInt32(out var pageSize) && pageSize > 2000)
            {
                return (false, new ValidationError
                {
                    Field = "pageSize",
                    Rule = "max_pagination_2000",
                    Message = "Page size exceeds Acumatica maximum of 2000 rows",
                    CurrentValue = pageSize.ToString(),
                    Expected = "Integer ≤ 2000"
                }, null);
            }
        }

        return (true, null, null);
    }

    private void ApplyFallbackRequiredFields(JsonElement request, string operation, List<ValidationError> errors, List<string> appliedRules)
    {
        var requiredFields = RequiredFieldsForOperation(operation);
        if (requiredFields.Count == 0)
        {
            return;
        }

        var missingFields = requiredFields
            .Where(field => !TryGetPropertyIgnoreCase(request, field, out _))
            .ToList();

        if (missingFields.Count == 0)
        {
            return;
        }

        errors.Add(new ValidationError
        {
            Field = string.Join(", ", missingFields),
            Rule = "required_fields",
            Message = $"Missing required fields: {string.Join(", ", missingFields)}",
            Expected = "Provide values for all required fields"
        });

        appliedRules.Add("required_fields");
    }

    private void ExtractFieldConstraints(JsonElement flowRoot, Dictionary<string, FieldConstraint> fieldConstraints, List<string> generalRules)
    {
        if (flowRoot.TryGetProperty("validationRules", out var rulesElement))
        {
            foreach (var ruleNode in rulesElement.EnumerateArray())
            {
                var rule = ruleNode.GetString();
                if (string.IsNullOrWhiteSpace(rule))
                {
                    continue;
                }

                var colonIndex = rule.IndexOf(':');
                if (colonIndex > 0)
                {
                    var fieldName = rule[..colonIndex].Trim();
                    var description = rule[(colonIndex + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        continue;
                    }

                    var constraint = GetOrCreateConstraint(fieldConstraints, fieldName);
                    constraint.Description = description;

                    if (description.Contains("required", StringComparison.OrdinalIgnoreCase))
                    {
                        constraint.Required = true;
                    }

                    var match = MaxLengthTextRegex.Match(description);
                    if (match.Success && int.TryParse(match.Groups["value"].Value, out var maxLength))
                    {
                        constraint.MaxLength = maxLength;
                    }

                    fieldConstraints[fieldName] = constraint;
                }
                else
                {
                    if (rule.Contains("2000", StringComparison.OrdinalIgnoreCase) && rule.Contains("page", StringComparison.OrdinalIgnoreCase))
                    {
                        generalRules.Add("max_pagination_2000");
                    }
                }
            }
        }

        if (flowRoot.TryGetProperty("constants", out var constantsElement))
        {
            foreach (var constant in constantsElement.EnumerateObject())
            {
                if (!TryParseMaxLengthConstant(constant.Name, constant.Value, out var fieldName, out var maxLength))
                {
                    continue;
                }

                var constraint = GetOrCreateConstraint(fieldConstraints, fieldName);
                if (!constraint.MaxLength.HasValue && maxLength.HasValue)
                {
                    constraint.MaxLength = maxLength;
                }

                fieldConstraints[fieldName] = constraint;
            }
        }
    }

    private void ApplyFieldConstraints(
        Dictionary<string, FieldConstraint> fieldConstraints,
        JsonElement request,
        List<ValidationError> errors,
        List<string> appliedRules)
    {
        foreach (var (fieldName, constraint) in fieldConstraints)
        {
            if (constraint.Required)
            {
                if (!TryGetPropertyIgnoreCase(request, fieldName, out var valueElement) ||
                    valueElement.ValueKind == JsonValueKind.Null ||
                    (valueElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valueElement.GetString())))
                {
                    errors.Add(new ValidationError
                    {
                        Field = fieldName,
                        Rule = "flow_required_field",
                        Message = $"{fieldName} is required",
                        Expected = "Provide a value"
                    });
                    appliedRules.Add($"flow_required:{fieldName}");
                    continue;
                }
            }

            if (constraint.MaxLength.HasValue && TryGetPropertyIgnoreCase(request, fieldName, out var fieldValue) && fieldValue.ValueKind == JsonValueKind.String)
            {
                var value = fieldValue.GetString() ?? string.Empty;
                if (value.Length > constraint.MaxLength.Value)
                {
                    errors.Add(new ValidationError
                    {
                        Field = fieldName,
                        Rule = "flow_max_length",
                        Message = $"{fieldName} exceeds {constraint.MaxLength.Value} character limit",
                        CurrentValue = value,
                        Expected = $"String with length ≤ {constraint.MaxLength.Value}"
                    });
                    appliedRules.Add($"flow_max_length:{fieldName}");
                }
            }
        }
    }

    private static bool TryParseMaxLengthConstant(string constantName, JsonElement constantElement, out string fieldName, out int? maxLength)
    {
        var match = MaxLengthConstantRegex.Match(constantName);
        if (!match.Success)
        {
            fieldName = string.Empty;
            maxLength = null;
            return false;
        }

        fieldName = ToCamelCaseField(match.Groups["field"].Value);
        maxLength = ExtractNumericValue(constantElement);
        return maxLength.HasValue;
    }

    private static int? ExtractNumericValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetInt32(out var number):
                return number;
            case JsonValueKind.String when int.TryParse(element.GetString(), out var parsed):
                return parsed;
            case JsonValueKind.Object when element.TryGetProperty("value", out var inner):
                return ExtractNumericValue(inner);
            default:
                return null;
        }
    }

    private static FieldConstraint GetOrCreateConstraint(Dictionary<string, FieldConstraint> constraints, string fieldName)
    {
        if (!constraints.TryGetValue(fieldName, out var constraint))
        {
            constraint = new FieldConstraint();
            constraints[fieldName] = constraint;
        }

        return constraint;
    }

    private static string ToCamelCaseField(string token)
    {
        var parts = token.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return token.ToLowerInvariant();
        }

        var first = parts[0].ToLowerInvariant();
        var rest = parts.Skip(1)
            .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant());

        return first + string.Concat(rest);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static readonly ConcurrentDictionary<string, List<string>> RequiredFieldsCache = new(StringComparer.OrdinalIgnoreCase);

    private List<string> RequiredFieldsForOperation(string operation)
    {
        return RequiredFieldsCache.GetOrAdd(operation, key =>
        {
            if (key.Contains("vendor", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "VendorID", "VendorName" };
            }

            if (key.Contains("payment", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "PaymentAmount" };
            }

            return new List<string>();
        });
    }

    private static readonly Regex MaxLengthTextRegex = new(@"\bmax\s+(?<value>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MaxLengthConstantRegex = new(@"^MAX_(?<field>.+?)_LENGTH$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class FieldConstraint
    {
        public bool Required { get; set; }
        public int? MaxLength { get; set; }
        public string? Description { get; set; }
    }
}
