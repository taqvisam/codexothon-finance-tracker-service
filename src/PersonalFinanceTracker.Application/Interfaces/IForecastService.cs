using PersonalFinanceTracker.Application.DTOs.Forecast;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IForecastService
{
    Task<ForecastMonthResponse> GetMonthForecastAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DailyForecastPoint>> GetDailyForecastAsync(Guid userId, CancellationToken ct = default);
}

