using PersonalFinanceTracker.Application.DTOs.Accounts;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IAccountService
{
    Task<IReadOnlyList<AccountResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken ct = default);
    Task<AccountResponse> UpdateAsync(Guid userId, Guid id, AccountRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task TransferAsync(Guid userId, TransferRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AccountMemberResponse>> GetMembersAsync(Guid userId, Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<AccountActivityResponse>> GetActivityAsync(Guid userId, Guid accountId, CancellationToken ct = default);
    Task InviteMemberAsync(Guid userId, Guid accountId, InviteAccountMemberRequest request, CancellationToken ct = default);
    Task UpdateMemberAsync(Guid userId, Guid accountId, Guid memberUserId, UpdateAccountMemberRequest request, CancellationToken ct = default);
}
