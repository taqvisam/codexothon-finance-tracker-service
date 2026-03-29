using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Accounts;

public record AccountRequest(string Name, AccountType Type, decimal OpeningBalance, decimal? CreditLimit, string? InstitutionName);
public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly Date, string? Note);
public record DeleteAccountRequest(bool DeleteRelatedData);
public record DeleteAccountImpactResponse(
    Guid AccountId,
    string AccountName,
    int TransactionCount,
    int GoalCount,
    int RecurringCount,
    int BudgetCount,
    bool RequiresCascadeDelete);
public record AccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    decimal OpeningBalance,
    decimal BalanceAtPeriodStart,
    decimal CurrentBalance,
    decimal? CreditLimit,
    decimal? AvailableCredit,
    string? InstitutionName,
    bool IsShared);
