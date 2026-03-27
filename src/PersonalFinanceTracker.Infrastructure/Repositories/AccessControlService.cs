using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Shared;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class AccessControlService(AppDbContext dbContext) : IAccessControlService
{
    public async Task<IReadOnlyList<Guid>> GetAccessibleAccountIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var owned = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var shared = await dbContext.AccountMembers
            .Where(x => x.UserId == userId)
            .Select(x => x.AccountId)
            .ToListAsync(ct);

        return owned.Concat(shared).Distinct().ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleAccountOwnerIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await GetAccessibleAccountIdsAsync(userId, ct);
        if (accessibleAccountIds.Count == 0)
        {
            return new List<Guid> { userId };
        }

        var ownerIds = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (!ownerIds.Contains(userId))
        {
            ownerIds.Add(userId);
        }

        return ownerIds;
    }

    public async Task<AccountAccessContext> GetAccountAccessAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        var owned = await dbContext.Accounts
            .Where(x => x.Id == accountId)
            .Select(x => new { x.Id, x.UserId })
            .FirstOrDefaultAsync(ct)
            ?? throw new AppException("Account not found.", 404);

        if (owned.UserId == userId)
        {
            return new AccountAccessContext(owned.Id, owned.UserId, AccountMemberRole.Owner, true);
        }

        var membership = await dbContext.AccountMembers
            .Where(x => x.AccountId == accountId && x.UserId == userId)
            .Select(x => (AccountMemberRole?)x.Role)
            .FirstOrDefaultAsync(ct);

        if (!membership.HasValue)
        {
            throw new AppException("You do not have access to this account.", 403);
        }

        return new AccountAccessContext(owned.Id, owned.UserId, membership.Value, false);
    }

    public async Task<AccountAccessContext> EnsureCanEditAccountAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        var access = await GetAccountAccessAsync(userId, accountId, ct);
        if (!access.CanEdit)
        {
            throw new AppException("You do not have edit access to this account.", 403);
        }

        return access;
    }
}
