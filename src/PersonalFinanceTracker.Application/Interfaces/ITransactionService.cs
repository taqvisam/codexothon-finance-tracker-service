using PersonalFinanceTracker.Application.DTOs.Transactions;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface ITransactionService
{
    Task<IReadOnlyList<TransactionResponse>> GetAllAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        Guid? accountId,
        Guid? categoryId,
        Domain.Enums.TransactionType? type,
        decimal? minAmount,
        decimal? maxAmount,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<TransactionResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<TransactionResponse> CreateAsync(Guid userId, TransactionRequest request, CancellationToken ct = default);
    Task<TransactionResponse> UpdateAsync(Guid userId, Guid id, TransactionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
