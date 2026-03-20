namespace PersonalFinanceTracker.Application.DTOs.Budgets;

public record BudgetRequest(Guid CategoryId, int Month, int Year, decimal Amount, int AlertThresholdPercent);
public record BudgetResponse(Guid Id, Guid CategoryId, int Month, int Year, decimal Amount, int AlertThresholdPercent, decimal SpentAmount);
