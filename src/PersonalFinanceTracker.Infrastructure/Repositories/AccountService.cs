using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Accounts;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class AccountService(
    AppDbContext dbContext,
    IAccessControlService accessControlService,
    AccountActivityLogger activityLogger) : IAccountService
{
    public async Task<IReadOnlyList<AccountResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        return await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new AccountResponse(x.Id, x.Name, x.Type, x.OpeningBalance, x.CurrentBalance, x.InstitutionName))
            .ToListAsync(ct);
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken ct = default)
    {
        var account = new Account
        {
            UserId = userId,
            Name = request.Name,
            Type = request.Type,
            OpeningBalance = request.OpeningBalance,
            CurrentBalance = request.OpeningBalance,
            InstitutionName = request.InstitutionName
        };

        dbContext.Accounts.Add(account);
        activityLogger.Log(account.Id, userId, "account", "created", $"Created account {account.Name}.", account.Id);
        await dbContext.SaveChangesAsync(ct);

        return new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance, account.CurrentBalance, account.InstitutionName);
    }

    public async Task<AccountResponse> UpdateAsync(Guid userId, Guid id, AccountRequest request, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Account not found.", 404);
        if (account.UserId != userId)
        {
            throw new AppException("Only account owner can update account settings.", 403);
        }

        account.Name = request.Name;
        account.Type = request.Type;
        account.InstitutionName = request.InstitutionName;
        account.LastUpdatedAt = DateTime.UtcNow;
        activityLogger.Log(account.Id, userId, "account", "updated", $"Updated account {account.Name}.", account.Id);
        await dbContext.SaveChangesAsync(ct);

        return new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance, account.CurrentBalance, account.InstitutionName);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Account not found.", 404);
        if (account.UserId != userId)
        {
            throw new AppException("Only account owner can delete account.", 403);
        }

        var hasTransactions = await dbContext.Transactions.AnyAsync(
            x => x.AccountId == id || x.TransferAccountId == id,
            ct
        );
        if (hasTransactions)
        {
            throw new AppException("Cannot delete account with existing transactions.");
        }

        var hasRecurring = await dbContext.RecurringTransactions.AnyAsync(x => x.AccountId == id, ct);
        if (hasRecurring)
        {
            throw new AppException("Cannot delete account linked to recurring transactions.");
        }

        var hasGoals = await dbContext.Goals.AnyAsync(x => x.LinkedAccountId == id, ct);
        if (hasGoals)
        {
            throw new AppException("Cannot delete account linked to goals.");
        }

        dbContext.AccountMembers.RemoveRange(dbContext.AccountMembers.Where(x => x.AccountId == id));
        dbContext.AccountActivities.RemoveRange(dbContext.AccountActivities.Where(x => x.AccountId == id));
        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task TransferAsync(Guid userId, TransferRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new AppException("Amount must be greater than zero.");
        if (request.FromAccountId == request.ToAccountId) throw new AppException("Source and destination accounts must differ.");

        var fromAccess = await accessControlService.EnsureCanEditAccountAsync(userId, request.FromAccountId, ct);
        var toAccess = await accessControlService.EnsureCanEditAccountAsync(userId, request.ToAccountId, ct);

        var from = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.FromAccountId, ct)
            ?? throw new AppException("Source account not found.", 404);
        var to = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.ToAccountId, ct)
            ?? throw new AppException("Destination account not found.", 404);

        if (from.CurrentBalance < request.Amount)
        {
            throw new AppException("Insufficient balance.");
        }

        from.CurrentBalance -= request.Amount;
        to.CurrentBalance += request.Amount;
        from.LastUpdatedAt = DateTime.UtcNow;
        to.LastUpdatedAt = DateTime.UtcNow;

        var transfer = new Transaction
        {
            UserId = userId,
            AccountId = from.Id,
            TransferAccountId = to.Id,
            Type = TransactionType.Transfer,
            Amount = request.Amount,
            TransactionDate = request.Date,
            Note = request.Note
        };

        dbContext.Transactions.Add(transfer);
        activityLogger.Log(from.Id, userId, "transaction", "transfer", $"Transferred {request.Amount:0.##} from {from.Name} to {to.Name}.", transfer.Id);
        if (from.Id != to.Id)
        {
            activityLogger.Log(to.Id, userId, "transaction", "transfer", $"Received transfer of {request.Amount:0.##} from {from.Name}.", transfer.Id);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccountMemberResponse>> GetMembersAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        await accessControlService.GetAccountAccessAsync(userId, accountId, ct);

        var account = await dbContext.Accounts
            .Where(x => x.Id == accountId)
            .Select(x => new { x.UserId })
            .FirstAsync(ct);

        var memberRows = await dbContext.AccountMembers
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var userIds = memberRows
            .Select(x => x.UserId)
            .Append(account.UserId)
            .Distinct()
            .ToList();

        var users = await dbContext.Users
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Email, x.DisplayName })
            .ToListAsync(ct);

        var userLookup = users.ToDictionary(x => x.Id);
        if (!userLookup.TryGetValue(account.UserId, out var ownerUser))
        {
            throw new AppException("Account owner not found.", 404);
        }

        var owner = new AccountMemberResponse(
            ownerUser.Id,
            ownerUser.Email,
            ResolveDisplayName(ownerUser.DisplayName, ownerUser.Email),
            AccountMemberRole.Owner,
            true);

        var members = memberRows
            .Where(x => x.UserId != account.UserId)
            .Select(member =>
            {
                userLookup.TryGetValue(member.UserId, out var memberUser);

                var email = memberUser?.Email ?? string.Empty;
                return new AccountMemberResponse(
                    member.UserId,
                    email,
                    ResolveDisplayName(memberUser?.DisplayName, email),
                    member.Role,
                    false);
            })
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new[] { owner }.Concat(members).ToList();
    }

    public async Task<IReadOnlyList<AccountActivityResponse>> GetActivityAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        await accessControlService.GetAccountAccessAsync(userId, accountId, ct);

        var activities = await dbContext.AccountActivities
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .ToListAsync(ct);

        if (activities.Count == 0)
        {
            return [];
        }

        var actorIds = activities
            .Select(activity => activity.ActorUserId)
            .Distinct()
            .ToList();

        var actors = actorIds.Count == 0
            ? []
            : await dbContext.Users
                .AsNoTracking()
                .Where(x => actorIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Email, x.DisplayName })
                .ToListAsync(ct);

        var actorLookup = actors.ToDictionary(x => x.Id);

        return activities
            .Select(activity =>
            {
                actorLookup.TryGetValue(activity.ActorUserId, out var actor);
                return new AccountActivityResponse(
                    activity.Id,
                    ResolveDisplayName(actor?.DisplayName, actor?.Email),
                    activity.EntityType,
                    activity.Action,
                    activity.Description,
                    activity.CreatedAt);
            })
            .ToList();
    }

    public async Task InviteMemberAsync(Guid userId, Guid accountId, InviteAccountMemberRequest request, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == accountId, ct)
            ?? throw new AppException("Account not found.", 404);
        if (account.UserId != userId)
        {
            throw new AppException("Only account owner can invite members.", 403);
        }

        var invitedUser = await dbContext.Users
            .FirstOrDefaultAsync(x => x.Email.ToLower() == request.Email.Trim().ToLower(), ct)
            ?? throw new AppException("Invited email must belong to a registered user.", 404);

        if (invitedUser.Id == account.UserId)
        {
            throw new AppException("Account owner is already a member.", 400);
        }

        var exists = await dbContext.AccountMembers.AnyAsync(x => x.AccountId == accountId && x.UserId == invitedUser.Id, ct);
        if (exists)
        {
            throw new AppException("User is already a member of this account.", 409);
        }

        dbContext.AccountMembers.Add(new AccountMember
        {
            AccountId = accountId,
            UserId = invitedUser.Id,
            Role = request.Role
        });
        activityLogger.Log(accountId, userId, "membership", "invited", $"Invited {invitedUser.DisplayName} as {request.Role}.", invitedUser.Id);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberAsync(Guid userId, Guid accountId, Guid memberUserId, UpdateAccountMemberRequest request, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == accountId, ct)
            ?? throw new AppException("Account not found.", 404);
        if (account.UserId != userId)
        {
            throw new AppException("Only account owner can manage roles.", 403);
        }

        if (memberUserId == account.UserId)
        {
            throw new AppException("Owner role cannot be changed.", 400);
        }

        var member = await dbContext.AccountMembers.FirstOrDefaultAsync(x => x.AccountId == accountId && x.UserId == memberUserId, ct)
            ?? throw new AppException("Member not found.", 404);

        member.Role = request.Role;
        activityLogger.Log(accountId, userId, "membership", "role-updated", $"Updated member role to {request.Role}.", memberUserId);
        await dbContext.SaveChangesAsync(ct);
    }

    private static string ResolveDisplayName(string? displayName, string? email)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Trim();
        }

        return "Unknown user";
    }
}
