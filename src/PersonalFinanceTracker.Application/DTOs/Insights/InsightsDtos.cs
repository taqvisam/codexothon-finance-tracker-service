namespace PersonalFinanceTracker.Application.DTOs.Insights;

public record HealthScoreFactor(string Name, decimal Score, string Description);

public record HealthScoreResponse(
    decimal Score,
    IReadOnlyList<HealthScoreFactor> Breakdown,
    IReadOnlyList<string> Suggestions);

