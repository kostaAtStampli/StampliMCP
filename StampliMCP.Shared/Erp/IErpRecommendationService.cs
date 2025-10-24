using StampliMCP.Shared.Models;

namespace StampliMCP.Shared.Erp;

public interface IErpRecommendationService
{
    Task<FlowRecommendation> RecommendAsync(string useCase, CancellationToken ct);
}
