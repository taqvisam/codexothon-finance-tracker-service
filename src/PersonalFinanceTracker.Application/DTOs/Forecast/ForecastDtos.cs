namespace PersonalFinanceTracker.Application.DTOs.Forecast;

public record ForecastMonthResponse(
    int Year,
    int Month,
    decimal CurrentBalance,
    decimal ProjectedIncome,
    decimal ProjectedExpense,
    decimal UpcomingKnownExpenses,
    decimal ForecastedEndBalance,
    decimal SafeToSpend,
    decimal ConfidenceScore,
    string Model,
    string? EstimatedNegativeDate,
    IReadOnlyList<string> RiskWarnings);

public record DailyForecastPoint(string Date, decimal ProjectedBalance);
