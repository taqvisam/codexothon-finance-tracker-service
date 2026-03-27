using PersonalFinanceTracker.Application.DTOs.Transactions;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IRuleEngineService
{
    Task<TransactionResponse> ApplyAsync(Guid userId, Guid accountOwnerId, TransactionRequest request, CancellationToken ct = default);
}
