using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Accounts;

public record AccountRequest(string Name, AccountType Type, decimal OpeningBalance, string? InstitutionName);
public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly Date, string? Note);
public record AccountResponse(Guid Id, string Name, AccountType Type, decimal OpeningBalance, decimal CurrentBalance, string? InstitutionName);
