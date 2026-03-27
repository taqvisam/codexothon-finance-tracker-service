using PersonalFinanceTracker.Application.DTOs.Shared;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IAccessControlService
{
    Task<IReadOnlyList<Guid>> GetAccessibleAccountIdsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAccessibleAccountOwnerIdsAsync(Guid userId, CancellationToken ct = default);
    Task<AccountAccessContext> GetAccountAccessAsync(Guid userId, Guid accountId, CancellationToken ct = default);
    Task<AccountAccessContext> EnsureCanEditAccountAsync(Guid userId, Guid accountId, CancellationToken ct = default);
}
