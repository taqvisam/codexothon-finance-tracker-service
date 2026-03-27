using PersonalFinanceTracker.Application.DTOs.Insights;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IInsightsService
{
    Task<HealthScoreResponse> GetHealthScoreAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<InsightHighlightResponse>> GetHighlightsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, CancellationToken ct = default);
}
