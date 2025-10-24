using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using StampliMCP.Shared.Erp;
using StampliMCP.Shared.Models;
using AcumaticaErrorCatalog = StampliMCP.McpServer.Acumatica.Models.ErrorCatalog;
using AcumaticaErrorDetail = StampliMCP.McpServer.Acumatica.Models.ErrorDetail;
using AcumaticaApiError = StampliMCP.McpServer.Acumatica.Models.ApiError;

namespace StampliMCP.McpServer.Acumatica.Services;

public sealed class AcumaticaDiagnosticService : IErpDiagnosticService
{
    private readonly ILogger<AcumaticaDiagnosticService> _logger;
    private readonly AcumaticaKnowledgeService _knowledge;
    private readonly AcumaticaFlowService _flows;

    public AcumaticaDiagnosticService(
        ILogger<AcumaticaDiagnosticService> logger,
        AcumaticaKnowledgeService knowledge,
        AcumaticaFlowService flows)
    {
        _logger = logger;
        _knowledge = knowledge;
        _flows = flows;
    }

    public async Task<ErrorDiagnostic> DiagnoseAsync(string errorMessage, CancellationToken ct)
    {
        var catalog = await _knowledge.GetErrorCatalogAsync(ct);
        var normalizedMessage = (errorMessage ?? string.Empty).Trim();
        var normalizedLower = NormalizePattern(normalizedMessage);

        var diagnostic = new ErrorDiagnostic
        {
            ErrorMessage = errorMessage,
            ErrorCategory = "Unknown",
            PossibleCauses = new List<string>(),
            Solutions = new List<ErrorSolution>(),
            RelatedFlowRules = new List<string>(),
            PreventionTips = new List<string>(),
            NextActions = new List<ResourceLinkBlock>()
        };

        if (string.IsNullOrWhiteSpace(normalizedLower))
        {
            diagnostic.Summary = "No error message supplied";
            return diagnostic;
        }

        var matchFound = false;

        // Authentication catalog
        if (catalog.AuthenticationErrors is not null)
        {
            foreach (var authError in catalog.AuthenticationErrors)
            {
                if (!IsMatch(authError.Message, normalizedLower)) continue;
                matchFound = true;
                diagnostic.ErrorCategory = "Authentication";
                diagnostic.PossibleCauses.Add(authError.Message);
                diagnostic.Solutions.Add(BuildSolution(authError));
                AddLocationTip(diagnostic, authError);
            }
        }

        // Operation catalog
        if (catalog.OperationErrors is not null)
        {
            foreach (var (operation, errorSet) in catalog.OperationErrors)
            {
                if (errorSet.Validation is not null)
                {
                    foreach (var detail in errorSet.Validation)
                    {
                        if (!IsMatch(detail.Message, normalizedLower)) continue;
                        matchFound = true;
                        diagnostic.ErrorCategory = "Validation";
                        diagnostic.PossibleCauses.Add(detail.Message);
                        diagnostic.RelatedFlowRules.Add($"{operation}: {detail.Message}");
                        diagnostic.Solutions.Add(BuildSolution(detail));
                        AddLocationTip(diagnostic, detail);
                        await AddFlowLinkAsync(diagnostic, operation, ct);
                    }
                }

                if (errorSet.BusinessLogic is not null)
                {
                    foreach (var detail in errorSet.BusinessLogic)
                    {
                        if (!IsMatch(detail.Message, normalizedLower)) continue;
                        matchFound = true;
                        diagnostic.ErrorCategory = "BusinessLogic";
                        diagnostic.PossibleCauses.Add(detail.Message);
                        diagnostic.Solutions.Add(BuildSolution(detail));
                        AddLocationTip(diagnostic, detail);
                        await AddFlowLinkAsync(diagnostic, operation, ct);
                    }
                }
            }
        }

        // API catalog
        if (catalog.ApiErrors is not null)
        {
            foreach (var apiError in catalog.ApiErrors)
            {
                if (!IsMatch(apiError.Message, normalizedLower) && !normalizedLower.Contains(apiError.Code.ToString()))
                {
                    continue;
                }

                matchFound = true;
                diagnostic.ErrorCategory = "API";
                diagnostic.PossibleCauses.Add(apiError.Message);
                if (!string.IsNullOrWhiteSpace(apiError.Handling))
                {
                    diagnostic.Solutions.Add(new ErrorSolution
                    {
                        Description = apiError.Handling,
                        CodeExample = null,
                        FlowReference = null
                    });
                }

                if (apiError.Location is not null)
                {
                    diagnostic.PreventionTips.Add($"Review {apiError.Location.File} ({apiError.Location.Lines})");
                }
            }
        }

        if (!matchFound)
        {
            diagnostic.Summary = "No catalog match found. Search knowledge base for broader guidance.";
            diagnostic.NextActions.Add(new ResourceLinkBlock
            {
                Uri = "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=errors",
                Name = "Search Acumatica knowledge base"
            });
            _logger.LogInformation("No diagnostic match for message: {Message}", errorMessage);
            return diagnostic;
        }

        diagnostic.Summary = $"Identified {diagnostic.ErrorCategory} issue based on known Acumatica patterns.";
        diagnostic.NextActions.Add(new ResourceLinkBlock
        {
            Uri = "mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query=errors",
            Name = "Search related knowledge"
        });

        return diagnostic;
    }

    private static string NormalizePattern(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var sanitized = Regex.Replace(text, @"\{.*?\}", string.Empty);
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        return sanitized.ToLowerInvariant();
    }

    private static bool IsMatch(string template, string targetLower)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var pattern = NormalizePattern(template);
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return targetLower.Contains(pattern);
    }

    private static ErrorSolution BuildSolution(AcumaticaErrorDetail detail)
    {
        var description = detail.Message;
        if (!string.IsNullOrWhiteSpace(detail.Condition) && !string.IsNullOrWhiteSpace(detail.Field))
        {
            description = $"{detail.Field}: {detail.Condition}";
        }

        return new ErrorSolution
        {
            Description = description,
            CodeExample = detail.TestAssertion,
            FlowReference = detail.Location?.File
        };
    }

    private static void AddLocationTip(ErrorDiagnostic diagnostic, AcumaticaErrorDetail detail)
    {
        if (detail.Location is null)
        {
            return;
        }

        var tip = $"Inspect {detail.Location.File}";
        if (!string.IsNullOrWhiteSpace(detail.Location.Lines))
        {
            tip += $" (lines {detail.Location.Lines})";
        }

        diagnostic.PreventionTips.Add(tip);
    }

    private async Task AddFlowLinkAsync(ErrorDiagnostic diagnostic, string operation, CancellationToken ct)
    {
        try
        {
            var flowName = await _flows.GetFlowForOperationAsync(operation, ct);
            if (string.IsNullOrWhiteSpace(flowName))
            {
                return;
            }

            var linkUri = $"mcp://stampli-unified/erp__query_knowledge?erp=acumatica&query={Uri.EscapeDataString(flowName)}&scope=flows";
            if (diagnostic.NextActions.Any(a => string.Equals(a.Uri, linkUri, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            diagnostic.NextActions.Add(new ResourceLinkBlock
            {
                Uri = linkUri,
                Name = $"Search flow guidance for {flowName}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to append flow link for operation {Operation}", operation);
        }
    }
}
