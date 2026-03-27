using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Transactions;

public record TransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Merchant,
    string? Note,
    string? PaymentMethod,
    Guid? TransferAccountId,
    List<string>? Tags);

public record TransactionImportRequest(IReadOnlyList<TransactionRequest> Items);

public record TransactionImportResponse(
    int ImportedCount,
    IReadOnlyList<string> Alerts);

public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string? Merchant,
    string? Note,
    string? PaymentMethod,
    Guid? TransferAccountId,
    List<string> Tags,
    List<string>? Alerts = null);
