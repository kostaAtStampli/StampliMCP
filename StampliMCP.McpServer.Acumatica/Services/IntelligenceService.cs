using StampliMCP.McpServer.Acumatica.Models;
using System.Text.RegularExpressions;

namespace StampliMCP.McpServer.Acumatica.Services;

/// <summary>
/// Provides intelligent analysis and recommendations based on Acumatica knowledge
/// </summary>
public class IntelligenceService
{
    private readonly KnowledgeService _knowledge;

    public IntelligenceService(KnowledgeService knowledge)
    {
        _knowledge = knowledge;
    }

    public async Task<object> AnalyzeIntegrationComplexity(string featureDescription, CancellationToken ct = default)
    {
        var allOperations = await _knowledge.GetAllOperationsAsync(ct);
        var categories = await _knowledge.GetCategoriesAsync(ct);

        // Identify relevant operations based on keywords
        var relevantOps = IdentifyRelevantOperations(featureDescription, allOperations);

        // Calculate complexity score
        var complexity = CalculateComplexity(relevantOps, featureDescription);

        // Identify dependencies and order
        var orderedOps = OrderOperationsByDependency(relevantOps);

        // Identify risks
        var risks = IdentifyRisks(relevantOps, featureDescription);

        return new
        {
            complexity = complexity.Level,
            estimatedEffort = complexity.EffortHours,
            requiredOperations = orderedOps.Select((op, index) => new
            {
                operation = op.Method,
                purpose = DeterminePurpose(op, featureDescription),
                order = index + 1,
                critical = IsCriticalOperation(op, featureDescription),
                category = op.Category
            }).ToList(),
            dependencies = ExtractDependencies(orderedOps, featureDescription),
            risks = risks,
            technicalRequirements = ExtractTechnicalRequirements(relevantOps, featureDescription),
            testingStrategy = GenerateTestingStrategy(relevantOps, complexity.Level),
            implementationNotes = GenerateImplementationNotes(relevantOps, featureDescription)
        };
    }

    public async Task<object> TroubleshootError(string errorMessage, CancellationToken ct = default)
    {
        var allOperations = await _knowledge.GetAllOperationsAsync(ct);

        // Find operation that produces this error
        var matchingOp = await FindOperationByErrorAsync(errorMessage, allOperations, ct);

        if (matchingOp == null)
        {
            return new
            {
                errorType = "UNKNOWN",
                message = $"Error message '{errorMessage}' not found in knowledge base",
                suggestion = "Check operation documentation or error catalog for similar errors"
            };
        }

        // Extract field information if it's a validation error
        var fieldInfo = ExtractFieldInfo(errorMessage, matchingOp);

        return new
        {
            errorType = DetermineErrorType(errorMessage, matchingOp),
            operation = matchingOp.Method,
            rootCause = GenerateRootCause(errorMessage, matchingOp, fieldInfo),
            affectedField = fieldInfo,
            immediateFixSteps = GenerateFixSteps(errorMessage, matchingOp, fieldInfo),
            preventionStrategy = GeneratePreventionStrategy(errorMessage, matchingOp, fieldInfo),
            relatedErrors = await GetRelatedErrorsAsync(matchingOp, ct),
            documentationLink = $"Knowledge/operations/{matchingOp.Category}.json:{matchingOp.Method}",
            exampleValidData = GenerateExampleValidData(matchingOp)
        };
    }

    public async Task<object> RecommendOperation(string businessRequirement, CancellationToken ct = default)
    {
        var allOperations = await _knowledge.GetAllOperationsAsync(ct);

        // Find best matching operations
        var matches = ScoreOperations(businessRequirement, allOperations);

        if (matches.Count == 0 || matches[0].Score < 0.1)
        {
            return new
            {
                message = "No matching operations found",
                suggestion = "Try rephrasing the requirement or browse categories"
            };
        }

        var topMatch = matches[0];

        var alternatives = matches.Skip(1).Take(2).ToList();

        return new
        {
            primaryRecommendation = new
            {
                operation = topMatch.Operation.Method,
                confidence = topMatch.Score > 0.7 ? "HIGH" : topMatch.Score > 0.4 ? "MEDIUM" : "LOW",
                reason = topMatch.Reason,
                category = topMatch.Operation.Category,
                usagePattern = GenerateUsagePattern(topMatch.Operation, businessRequirement)
            },
            alternatives = alternatives.Select(alt => new
            {
                operation = alt.Operation.Method,
                confidence = alt.Score > 0.7 ? "HIGH" : alt.Score > 0.4 ? "MEDIUM" : "LOW",
                reason = alt.Reason,
                tradeoff = GenerateTradeoff(topMatch.Operation, alt.Operation)
            }).ToList(),
            implementationApproach = GenerateImplementationApproach(topMatch.Operation, businessRequirement),
            relatedOperations = GetRelatedOperations(topMatch.Operation, allOperations),
            businessConsiderations = GenerateBusinessConsiderations(topMatch.Operation, businessRequirement),
            estimatedComplexity = EstimateComplexity(topMatch.Operation, businessRequirement)
        };
    }

    public async Task<object> GenerateTestScenarios(string operationName, CancellationToken ct = default)
    {
        var operation = await _knowledge.FindOperationAsync(operationName, ct);

        if (operation == null)
        {
            return new
            {
                error = $"Operation '{operationName}' not found",
                suggestion = "Use search_operations to find available operations"
            };
        }

        var happyPath = GenerateHappyPathTests(operation);
        var edgeCases = GenerateEdgeCaseTests(operation);
        var errorCases = await GenerateErrorCaseTestsAsync(operation, ct);
        var performanceCases = GeneratePerformanceTests(operation);
        var securityCases = GenerateSecurityTests(operation);

        var totalScenarios = happyPath.Count + edgeCases.Count + errorCases.Count + performanceCases.Count + securityCases.Count;

        return new
        {
            operation = operation.Method,
            category = operation.Category,
            testCategories = new
            {
                happyPath,
                edgeCases,
                errorCases,
                performanceCases,
                securityCases
            },
            totalScenarios,
            estimatedTestTime = $"{totalScenarios * 15 / 60}-{totalScenarios * 20 / 60} hours",
            automationRecommendation = $"Automate P0/P1 ({happyPath.Count + errorCases.Count} tests), manual P2 ({edgeCases.Count + performanceCases.Count + securityCases.Count} tests)",
            cicdIntegration = $"Run on every commit to {operation.Category}-related code"
        };
    }

    // Helper methods
    private List<Operation> IdentifyRelevantOperations(string description, List<Operation> allOps)
    {
        var keywords = ExtractKeywords(description.ToLower());
        var relevant = new List<(Operation op, int score)>();

        foreach (var op in allOps)
        {
            int score = 0;
            var searchText = $"{op.Method} {op.Summary} {op.Category}".ToLower();

            foreach (var keyword in keywords)
            {
                if (searchText.Contains(keyword))
                    score += keyword.Length; // Longer keywords = more specific = higher score
            }

            if (score > 0)
                relevant.Add((op, score));
        }

        return relevant.OrderByDescending(r => r.score).Take(5).Select(r => r.op).ToList();
    }

    private string[] ExtractKeywords(string text)
    {
        // Common words to ignore
        var stopWords = new HashSet<string> { "a", "an", "the", "to", "for", "with", "and", "or", "in", "on", "at", "of", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "should", "could", "may", "might", "must", "can" };

        return Regex.Split(text, @"\W+")
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToArray();
    }

    private (string Level, string EffortHours) CalculateComplexity(List<Operation> ops, string description)
    {
        int complexity = ops.Count;

        // Adjust for keywords indicating complexity
        if (description.ToLower().Contains("batch") || description.ToLower().Contains("bulk"))
            complexity += 2;
        if (description.ToLower().Contains("workflow") || description.ToLower().Contains("approval"))
            complexity += 3;
        if (description.ToLower().Contains("notification") || description.ToLower().Contains("email"))
            complexity += 1;
        if (description.ToLower().Contains("duplicate") || description.ToLower().Contains("check"))
            complexity += 1;

        return complexity switch
        {
            <= 2 => ("LOW", "2-4 hours"),
            <= 4 => ("MEDIUM", "8-12 hours"),
            <= 6 => ("HIGH", "16-24 hours"),
            _ => ("VERY_HIGH", "32+ hours")
        };
    }

    private List<Operation> OrderOperationsByDependency(List<Operation> ops)
    {
        // Simple heuristic: check/get operations before create/update operations
        var ordered = new List<Operation>();

        // First: validation/check operations (contains "get", "retrieve", "check", "matching")
        ordered.AddRange(ops.Where(op =>
            op.Method.ToLower().Contains("get") ||
            op.Method.ToLower().Contains("retrieve") ||
            op.Method.ToLower().Contains("check") ||
            op.Method.ToLower().Contains("matching")));

        // Then: create/update operations
        ordered.AddRange(ops.Where(op =>
            op.Method.ToLower().Contains("export") ||
            op.Method.ToLower().Contains("create") ||
            op.Method.ToLower().Contains("update")).Except(ordered));

        // Finally: any remaining operations
        ordered.AddRange(ops.Except(ordered));

        return ordered;
    }

    private string DeterminePurpose(Operation op, string description)
    {
        var method = op.Method.ToLower();
        var desc = description.ToLower();

        if (method.Contains("get") || method.Contains("retrieve"))
        {
            if (desc.Contains("duplicate")) return "Duplicate detection";
            if (desc.Contains("validate")) return "Validation";
            return "Data retrieval";
        }

        if (method.Contains("export") || method.Contains("create"))
        {
            if (desc.Contains("vendor")) return "Create vendor";
            if (desc.Contains("bill") || desc.Contains("invoice")) return "Create bill/invoice";
            if (desc.Contains("payment")) return "Create payment";
            return "Data creation";
        }

        return op.Summary ?? "Operation execution";
    }

    private bool IsCriticalOperation(Operation op, string description)
    {
        var critical = new[] { "export", "create", "update", "delete", "payment" };
        return critical.Any(c => op.Method.ToLower().Contains(c));
    }

    private List<string> ExtractDependencies(List<Operation> ops, string description)
    {
        var dependencies = new List<string>();

        if (ops.Count > 1)
        {
            var checks = ops.Where(o => o.Method.ToLower().Contains("get") || o.Method.ToLower().Contains("check")).ToList();
            var creates = ops.Where(o => o.Method.ToLower().Contains("export") || o.Method.ToLower().Contains("create")).ToList();

            if (checks.Any() && creates.Any())
                dependencies.Add($"Must {string.Join(" and ", checks.Select(c => c.Method))} BEFORE {string.Join(" and ", creates.Select(c => c.Method))}");
        }

        if (description.ToLower().Contains("email") || description.ToLower().Contains("notification"))
            dependencies.Add("Email system integration required (external)");

        if (description.ToLower().Contains("approval") || description.ToLower().Contains("workflow"))
            dependencies.Add("Workflow approval system required");

        return dependencies;
    }

    private List<object> IdentifyRisks(List<Operation> ops, string description)
    {
        var risks = new List<object>();

        if (description.ToLower().Contains("duplicate") && ops.Any(o => o.Method.ToLower().Contains("export")))
        {
            risks.Add(new
            {
                risk = "Race condition on duplicate check",
                severity = "HIGH",
                mitigation = "Use transaction locks or unique constraints in database"
            });
        }

        if (description.ToLower().Contains("bulk") || description.ToLower().Contains("batch"))
        {
            risks.Add(new
            {
                risk = "Partial failure in batch operation",
                severity = "MEDIUM",
                mitigation = "Implement transaction rollback or compensating actions"
            });
        }

        if (ops.Count > 3)
        {
            risks.Add(new
            {
                risk = "Complex orchestration increases failure points",
                severity = "MEDIUM",
                mitigation = "Implement retry logic and comprehensive error handling"
            });
        }

        return risks;
    }

    private List<string> ExtractTechnicalRequirements(List<Operation> ops, string description)
    {
        var requirements = new List<string>();

        if (description.ToLower().Contains("transaction") || description.ToLower().Contains("batch"))
            requirements.Add("Database transaction support");

        if (description.ToLower().Contains("email") || description.ToLower().Contains("notification"))
            requirements.Add("Email service configuration (SMTP/SendGrid)");

        if (ops.Count > 1)
            requirements.Add("Error handling for partial failures");

        if (description.ToLower().Contains("async") || description.ToLower().Contains("background"))
            requirements.Add("Background job processing (Hangfire/Quartz)");

        requirements.Add($"Acumatica API access with {string.Join(", ", ops.Select(o => o.Category).Distinct())} permissions");

        return requirements;
    }

    private string GenerateTestingStrategy(List<Operation> ops, string complexity)
    {
        var strategy = $"Test {string.Join(", ", ops.Select(o => o.Method))} operations independently first. ";

        if (ops.Count > 1)
            strategy += "Then test integrated flow with end-to-end scenarios. ";

        if (complexity == "HIGH" || complexity == "VERY_HIGH")
            strategy += "Include load testing for performance validation. ";

        strategy += "Test error scenarios, rollback mechanisms, and edge cases.";

        return strategy;
    }

    private string GenerateImplementationNotes(List<Operation> ops, string description)
    {
        var notes = new List<string>();

        if (ops.Any(o => o.Method.ToLower().Contains("get") || o.Method.ToLower().Contains("retrieve")))
            notes.Add("Cache results of lookup operations to reduce API calls");

        if (description.ToLower().Contains("duplicate"))
            notes.Add("Consider using Acumatica's built-in duplicate detection if available");

        if (ops.Count > 2)
            notes.Add("Break into smaller microservices/modules for maintainability");

        return notes.Count > 0 ? string.Join(". ", notes) : "Follow standard SOLID principles and error handling patterns";
    }

    private async Task<Operation?> FindOperationByErrorAsync(string errorMessage, List<Operation> ops, CancellationToken ct)
    {
        foreach (var op in ops)
        {
            var errors = await _knowledge.GetOperationErrorsAsync(op.Method, ct);
            if (errors.Any(e => e.Message.Contains(errorMessage, StringComparison.OrdinalIgnoreCase)))
                return op;
        }
        return null;
    }

    private string DetermineErrorType(string errorMessage, Operation op)
    {
        if (errorMessage.Contains("required", StringComparison.OrdinalIgnoreCase))
            return "VALIDATION_ERROR_REQUIRED";
        if (errorMessage.Contains("exceed", StringComparison.OrdinalIgnoreCase) || errorMessage.Contains("length", StringComparison.OrdinalIgnoreCase))
            return "VALIDATION_ERROR_LENGTH";
        if (errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            return "VALIDATION_ERROR_FORMAT";
        if (errorMessage.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return "BUSINESS_LOGIC_ERROR";

        return "VALIDATION_ERROR";
    }

    private object? ExtractFieldInfo(string errorMessage, Operation op)
    {
        // Try to find field name in error message
        var words = errorMessage.Split(' ');
        var fieldName = words.FirstOrDefault(w => op.RequiredFields.ContainsKey(w));

        if (fieldName != null && op.RequiredFields.TryGetValue(fieldName, out var field))
        {
            return new
            {
                name = fieldName,
                maxLength = field.MaxLength,
                required = true,
                type = field.Type
            };
        }

        return null;
    }

    private string GenerateRootCause(string errorMessage, Operation op, object? fieldInfo)
    {
        if (errorMessage.Contains("required"))
            return $"Input validation failed: Required field missing. The {op.Method} operation requires all mandatory fields to be provided.";

        if (errorMessage.Contains("exceed") && fieldInfo != null)
        {
            var field = fieldInfo.GetType().GetProperty("name")?.GetValue(fieldInfo)?.ToString();
            var maxLength = fieldInfo.GetType().GetProperty("maxLength")?.GetValue(fieldInfo);
            return $"Input validation failed: {field} field limited to {maxLength} characters by Acumatica schema.";
        }

        return $"Operation {op.Method} validation failed: {errorMessage}";
    }

    private List<string> GenerateFixSteps(string errorMessage, Operation op, object? fieldInfo)
    {
        var steps = new List<string>();

        if (errorMessage.Contains("required"))
        {
            steps.Add("Ensure all required fields are provided in the request");
            steps.Add($"Check {op.Method} documentation for required field list");
            steps.Add("Validate input data before sending to Acumatica API");
        }
        else if (errorMessage.Contains("exceed") && fieldInfo != null)
        {
            var field = fieldInfo.GetType().GetProperty("name")?.GetValue(fieldInfo)?.ToString();
            var maxLength = fieldInfo.GetType().GetProperty("maxLength")?.GetValue(fieldInfo);
            steps.Add($"Truncate {field} to {maxLength} characters");
            steps.Add($"OR use alternative field for longer values");
            steps.Add($"Store full value in custom field if needed");
        }
        else
        {
            steps.Add("Review error message and check data format");
            steps.Add($"Consult {op.Method} documentation");
            steps.Add("Validate against operation requirements");
        }

        return steps;
    }

    private List<string> GeneratePreventionStrategy(string errorMessage, Operation op, object? fieldInfo)
    {
        var strategy = new List<string>();

        if (fieldInfo != null)
        {
            var field = fieldInfo.GetType().GetProperty("name")?.GetValue(fieldInfo)?.ToString();
            var maxLength = fieldInfo.GetType().GetProperty("maxLength")?.GetValue(fieldInfo);

            if (maxLength != null)
            {
                strategy.Add($"Add client-side validation: maxLength={maxLength}");
                strategy.Add("Show character counter in UI");
            }

            strategy.Add($"Add unit test: assert {field} meets requirements");
        }

        strategy.Add($"Implement comprehensive input validation before calling {op.Method}");
        strategy.Add("Add integration tests with invalid data scenarios");
        strategy.Add("Log validation failures for monitoring");

        return strategy;
    }

    private async Task<List<string>> GetRelatedErrorsAsync(Operation op, CancellationToken ct)
    {
        var errors = await _knowledge.GetOperationErrorsAsync(op.Method, ct);
        return errors.Take(3).Select(e => e.Message).ToList();
    }

    private object GenerateExampleValidData(Operation op)
    {
        var example = new Dictionary<string, object>();

        foreach (var field in op.RequiredFields.Take(3))
        {
            if (field.Value.Type == "string")
            {
                if (field.Value.MaxLength.HasValue)
                    example[field.Key] = $"Example{field.Key}";
                else
                    example[field.Key] = $"Sample {field.Key} value";
            }
            else if (field.Value.Type == "number")
            {
                example[field.Key] = 100;
            }
            else if (field.Value.Type == "boolean")
            {
                example[field.Key] = true;
            }
        }

        return example;
    }

    private List<(Operation Operation, double Score, string Reason)> ScoreOperations(string requirement, List<Operation> ops)
    {
        var keywords = ExtractKeywords(requirement.ToLower());
        var scored = new List<(Operation, double, string)>();

        foreach (var op in ops)
        {
            double score = 0;
            var reasons = new List<string>();
            var searchText = $"{op.Method} {op.Summary} {op.Category}".ToLower();

            foreach (var keyword in keywords)
            {
                if (searchText.Contains(keyword))
                {
                    score += 0.2;
                    reasons.Add($"Matches '{keyword}'");
                }
            }

            // Boost score for exact category matches
            if (keywords.Any(k => op.Category.ToLower().Contains(k)))
            {
                score += 0.3;
                reasons.Add($"Category match");
            }

            if (score > 0)
            {
                var reason = reasons.Count > 0 ? string.Join(", ", reasons) : "Keyword match";
                scored.Add((op, Math.Min(score, 1.0), reason));
            }
        }

        return scored.OrderByDescending(s => s.Item2).Take(3).ToList();
    }

    private string GenerateUsagePattern(Operation op, string requirement)
    {
        if (requirement.ToLower().Contains("batch") || requirement.ToLower().Contains("multiple"))
            return $"Call {op.Method} in loop with transaction wrapper for atomicity";

        if (op.Method.ToLower().Contains("get") || op.Method.ToLower().Contains("retrieve"))
            return $"Call {op.Method} before main operation for validation/lookup";

        return $"Direct call to {op.Method} with validated input data";
    }

    private string GenerateTradeoff(Operation primary, Operation alternative)
    {
        var primaryType = primary.Method.ToLower().Contains("get") ? "lookup" : "operation";
        var altType = alternative.Method.ToLower().Contains("get") ? "lookup" : "operation";

        if (primaryType == altType)
            return $"Similar functionality, check detailed requirements";

        return $"{alternative.Method}: Different approach, may have {(altType == "lookup" ? "better validation" : "more features")}";
    }

    private object GenerateImplementationApproach(Operation op, string requirement)
    {
        var steps = new List<string>();

        if (requirement.ToLower().Contains("multiple") || requirement.ToLower().Contains("batch"))
        {
            steps.Add("1. Validate all input data first");
            steps.Add("2. Begin database transaction");
            steps.Add($"3. Loop through items calling {op.Method}");
            steps.Add("4. Rollback transaction if any fails");
            steps.Add("5. Commit transaction if all succeed");
            steps.Add("6. Send notifications/confirmations");

            return new
            {
                architecture = "Batch processor with transaction rollback",
                steps,
                errorHandling = "Transaction-based: all-or-nothing approach"
            };
        }

        steps.Add($"1. Validate input data");
        steps.Add($"2. Call {op.Method}");
        steps.Add("3. Handle response");
        steps.Add("4. Log result");

        return new
        {
            architecture = "Simple request-response pattern",
            steps,
            errorHandling = "Standard try-catch with error response"
        };
    }

    private List<object> GetRelatedOperations(Operation op, List<Operation> allOps)
    {
        return allOps
            .Where(o => o.Category == op.Category && o.Method != op.Method)
            .Take(3)
            .Select(o => new
            {
                operation = o.Method,
                purpose = o.Summary ?? "Related operation"
            })
            .ToList<object>();
    }

    private List<string> GenerateBusinessConsiderations(Operation op, string requirement)
    {
        var considerations = new List<string>();

        if (requirement.ToLower().Contains("approval"))
            considerations.Add("Approval workflow: Who approves? Single or multi-level?");

        if (requirement.ToLower().Contains("payment"))
            considerations.Add("Payment limits: Any maximum amount per transaction?");

        if (requirement.ToLower().Contains("batch") || requirement.ToLower().Contains("multiple"))
            considerations.Add("Timing: Real-time vs scheduled batch processing?");

        if (op.Category.ToLower().Contains("vendor"))
            considerations.Add("Vendor onboarding: Approval required before activation?");

        considerations.Add("Error notification: Who gets notified of failures?");
        considerations.Add("Audit trail: What level of logging is required?");

        return considerations.Take(4).ToList();
    }

    private string EstimateComplexity(Operation op, string requirement)
    {
        int complexity = 1;

        if (requirement.ToLower().Contains("batch") || requirement.ToLower().Contains("multiple"))
            complexity += 2;

        if (requirement.ToLower().Contains("approval") || requirement.ToLower().Contains("workflow"))
            complexity += 3;

        if (op.RequiredFields.Count > 5)
            complexity += 1;

        return complexity switch
        {
            <= 2 => "LOW (straightforward implementation)",
            <= 4 => "MEDIUM (some orchestration needed)",
            _ => "HIGH (complex workflow with multiple components)"
        };
    }

    private List<object> GenerateHappyPathTests(Operation op)
    {
        var tests = new List<object>
        {
            new
            {
                scenario = $"Create/execute {op.Method} with all valid required fields",
                testData = GenerateExampleValidData(op),
                expectedResult = $"{op.Method} succeeds, returns expected response",
                priority = "P0"
            }
        };

        if (op.OptionalFields?.Any() == true)
        {
            tests.Add(new
            {
                scenario = $"{op.Method} with optional fields included",
                testData = new { note = "Including optional fields" },
                expectedResult = "Success with optional data processed",
                priority = "P1"
            });
        }

        return tests;
    }

    private List<object> GenerateEdgeCaseTests(Operation op)
    {
        var tests = new List<object>();

        foreach (var field in op.RequiredFields.Where(f => f.Value.MaxLength.HasValue).Take(2))
        {
            tests.Add(new
            {
                scenario = $"{field.Key} exactly at max length ({field.Value.MaxLength} characters)",
                testData = new Dictionary<string, object> { [field.Key] = new string('A', field.Value.MaxLength.Value) },
                expectedResult = "Success (boundary test)",
                priority = "P1"
            });
        }

        tests.Add(new
        {
            scenario = "Special characters in string fields",
            testData = new { note = "O'Reilly & Sons, Inc. (UTF-8)" },
            expectedResult = "Success (encoding test)",
            priority = "P1"
        });

        return tests;
    }

    private async Task<List<object>> GenerateErrorCaseTestsAsync(Operation op, CancellationToken ct)
    {
        var tests = new List<object>();
        var errors = await _knowledge.GetOperationErrorsAsync(op.Method, ct);

        foreach (var error in errors.Take(3))
        {
            tests.Add(new
            {
                scenario = $"Trigger validation error: {error.Message}",
                testData = new { note = "Invalid data to trigger error" },
                expectedError = error,
                priority = "P0"
            });
        }

        return tests;
    }

    private List<object> GeneratePerformanceTests(Operation op)
    {
        return new List<object>
        {
            new
            {
                scenario = $"Execute {op.Method} 100 times sequentially",
                metric = "Average response time < 500ms",
                priority = "P2"
            },
            new
            {
                scenario = $"Concurrent {op.Method} calls (10 parallel)",
                metric = "No timeout or connection errors",
                priority = "P2"
            }
        };
    }

    private List<object> GenerateSecurityTests(Operation op)
    {
        var tests = new List<object>();

        foreach (var field in op.RequiredFields.Where(f => f.Value.Type == "string").Take(2))
        {
            tests.Add(new
            {
                scenario = $"SQL injection attempt in {field.Key}",
                testData = new Dictionary<string, object> { [field.Key] = "'; DROP TABLE--" },
                expectedResult = "Properly escaped, no execution",
                priority = "P0"
            });

            tests.Add(new
            {
                scenario = $"XSS attempt in {field.Key}",
                testData = new Dictionary<string, object> { [field.Key] = "<script>alert('xss')</script>" },
                expectedResult = "Sanitized or escaped",
                priority = "P1"
            });
        }

        return tests;
    }
}
