using PersonalFinanceTracker.Application.DTOs.Insights;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IInsightsService
{
    Task<HealthScoreResponse> GetHealthScoreAsync(Guid userId, CancellationToken ct = default);
}

