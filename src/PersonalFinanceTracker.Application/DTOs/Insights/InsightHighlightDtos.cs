namespace PersonalFinanceTracker.Application.DTOs.Insights;

public record InsightHighlightResponse(
    string Title,
    string Message,
    string Severity,
    decimal ChangePercent,
    string PeriodLabel);
