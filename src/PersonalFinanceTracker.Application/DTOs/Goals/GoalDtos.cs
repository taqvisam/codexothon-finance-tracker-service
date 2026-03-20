namespace PersonalFinanceTracker.Application.DTOs.Goals;

public record GoalRequest(string Name, decimal TargetAmount, DateOnly? TargetDate, Guid? LinkedAccountId, string? Icon, string? Color);
public record GoalActionRequest(decimal Amount);
public record GoalResponse(Guid Id, string Name, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, string Status, decimal ProgressPercent);
