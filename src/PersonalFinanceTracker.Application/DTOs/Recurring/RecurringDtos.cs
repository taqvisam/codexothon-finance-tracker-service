using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Recurring;

public record RecurringRequest(string Title, TransactionType Type, decimal Amount, Guid? CategoryId, Guid? AccountId, RecurringFrequency Frequency, DateOnly StartDate, DateOnly? EndDate, DateOnly NextRunDate, bool AutoCreateTransaction, bool IsPaused);
public record RecurringResponse(Guid Id, string Title, TransactionType Type, decimal Amount, Guid? CategoryId, Guid? AccountId, RecurringFrequency Frequency, DateOnly NextRunDate, bool AutoCreateTransaction, bool IsPaused);
