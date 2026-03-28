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
    private sealed record AccountBalanceEvent(Guid AccountId, Guid? TransferAccountId, TransactionType Type, decimal Amount);

    public async Task<IReadOnlyList<AccountResponse>> GetAllAsync(Guid userId, DateOnly? from = null, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var balancesAtPeriodStart = CalculateBalancesAtPeriodStart(accounts, []);
        if (from.HasValue && accounts.Count > 0)
        {
            var priorTransactions = await dbContext.Transactions
                .AsNoTracking()
                .Where(x =>
                    x.TransactionDate < from.Value
                    && (accessibleAccountIds.Contains(x.AccountId)
                        || (x.TransferAccountId.HasValue && accessibleAccountIds.Contains(x.TransferAccountId.Value))))
                .Select(x => new AccountBalanceEvent(x.AccountId, x.TransferAccountId, x.Type, x.Amount))
                .ToListAsync(ct);

            balancesAtPeriodStart = CalculateBalancesAtPeriodStart(accounts, priorTransactions);
        }

        var collaboratorAccountIds = await dbContext.AccountMembers
            .AsNoTracking()
            .Where(x => accessibleAccountIds.Contains(x.AccountId))
            .Select(x => x.AccountId)
            .Distinct()
            .ToListAsync(ct);

        var collaboratorSet = collaboratorAccountIds.ToHashSet();

        return accounts
            .Select(account => ToResponse(
                account,
                account.UserId != userId || collaboratorSet.Contains(account.Id),
                balancesAtPeriodStart[account.Id]))
            .ToList();
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken ct = default)
    {
        EnsureValidAccountRequest(request);

        var account = new Account
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Type = request.Type,
            OpeningBalance = request.OpeningBalance,
            CurrentBalance = request.OpeningBalance,
            CreditLimit = NormalizeCreditLimit(request.Type, request.CreditLimit),
            InstitutionName = request.InstitutionName
        };

        dbContext.Accounts.Add(account);
        activityLogger.Log(account.Id, userId, "account", "created", $"Created account {account.Name}.", account.Id);
        await dbContext.SaveChangesAsync(ct);

        return ToResponse(account, false, account.OpeningBalance);
    }

    public async Task<AccountResponse> UpdateAsync(Guid userId, Guid id, AccountRequest request, CancellationToken ct = default)
    {
        EnsureValidAccountRequest(request);

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Account not found.", 404);
        if (account.UserId != userId)
        {
            throw new AppException("Only account owner can update account settings.", 403);
        }

        var openingBalanceDelta = request.OpeningBalance - account.OpeningBalance;
        account.Name = request.Name.Trim();
        account.Type = request.Type;
        account.OpeningBalance = request.OpeningBalance;
        account.CurrentBalance += openingBalanceDelta;
        account.CreditLimit = NormalizeCreditLimit(request.Type, request.CreditLimit);
        account.InstitutionName = request.InstitutionName;
        EnsureCurrentBalanceAllowed(account);
        account.LastUpdatedAt = DateTime.UtcNow;
        activityLogger.Log(account.Id, userId, "account", "updated", $"Updated account {account.Name}.", account.Id);
        await dbContext.SaveChangesAsync(ct);

        return ToResponse(account, false, account.OpeningBalance);
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

        EnsureSufficientSourceCapacity(from, request.Amount);

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
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                await accessControlService.GetAccountAccessAsync(userId, accountId, ct);

                var account = await dbContext.Accounts
                    .AsNoTracking()
                    .Where(x => x.Id == accountId)
                    .Select(x => new { x.UserId })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new AppException("Account not found.", 404);

                var memberRows = await dbContext.AccountMembers
                    .AsNoTracking()
                    .Where(x => x.AccountId == accountId)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(ct);

                var userIds = memberRows
                    .Select(x => x.UserId)
                    .Append(account.UserId)
                    .Distinct()
                    .ToList();

                var users = await dbContext.Users
                    .AsNoTracking()
                    .Where(x => userIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Email, x.DisplayName })
                    .ToListAsync(ct);

                var userLookup = users.ToDictionary(x => x.Id);
                userLookup.TryGetValue(account.UserId, out var ownerUser);

                var ownerEmail = ownerUser?.Email ?? string.Empty;
                var owner = new AccountMemberResponse(
                    account.UserId,
                    ownerEmail,
                    ResolveDisplayName(ownerUser?.DisplayName, ownerEmail),
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
            catch (AppException)
            {
                throw;
            }
            catch when (attempt == 1)
            {
                await Task.Delay(1000, ct);
            }
        }

        throw new AppException("Unable to load shared account members.", 500);
    }

    public async Task<IReadOnlyList<AccountActivityResponse>> GetActivityAsync(Guid userId, Guid accountId, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
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
            catch (AppException)
            {
                throw;
            }
            catch when (attempt == 1)
            {
                await Task.Delay(1000, ct);
            }
        }

        throw new AppException("Unable to load shared account activity.", 500);
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

    private static AccountResponse ToResponse(Account account, bool isShared, decimal balanceAtPeriodStart)
    {
        var availableCredit = GetAvailableCredit(account);
        return new AccountResponse(
            account.Id,
            account.Name,
            account.Type,
            account.OpeningBalance,
            balanceAtPeriodStart,
            account.CurrentBalance,
            account.CreditLimit,
            availableCredit,
            account.InstitutionName,
            isShared);
    }

    private static Dictionary<Guid, decimal> CalculateBalancesAtPeriodStart(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<AccountBalanceEvent> transactions)
    {
        var balances = accounts.ToDictionary(account => account.Id, account => account.OpeningBalance);
        if (transactions.Count == 0)
        {
            return balances;
        }

        foreach (var transaction in transactions)
        {
            var sourceAccountId = transaction.AccountId;
            var transferAccountId = transaction.TransferAccountId;
            var type = transaction.Type;
            var amount = transaction.Amount;

            if (type == TransactionType.Income)
            {
                if (balances.ContainsKey(sourceAccountId))
                {
                    balances[sourceAccountId] += amount;
                }

                continue;
            }

            if (type == TransactionType.Expense)
            {
                if (balances.ContainsKey(sourceAccountId))
                {
                    balances[sourceAccountId] -= amount;
                }

                continue;
            }

            if (type != TransactionType.Transfer)
            {
                continue;
            }

            if (balances.ContainsKey(sourceAccountId))
            {
                balances[sourceAccountId] -= amount;
            }

            if (transferAccountId.HasValue && balances.ContainsKey(transferAccountId.Value))
            {
                balances[transferAccountId.Value] += amount;
            }
        }

        return balances;
    }

    private static void EnsureValidAccountRequest(AccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("Account name is required.", 400);
        }

        if (request.OpeningBalance < 0)
        {
            throw new AppException("Opening balance cannot be negative.", 400);
        }

        if (request.Type == AccountType.CreditCard && (!request.CreditLimit.HasValue || request.CreditLimit.Value <= 0))
        {
            throw new AppException("Credit limit is required for credit card accounts.", 400);
        }
    }

    private static decimal? NormalizeCreditLimit(AccountType type, decimal? creditLimit)
    {
        if (type != AccountType.CreditCard)
        {
            return null;
        }

        return Math.Round(creditLimit!.Value, 2);
    }

    private static decimal? GetAvailableCredit(Account account)
    {
        if (account.Type != AccountType.CreditCard || !account.CreditLimit.HasValue)
        {
            return null;
        }

        return account.CreditLimit.Value + account.CurrentBalance;
    }

    private static void EnsureCurrentBalanceAllowed(Account account)
    {
        if (account.Type != AccountType.CreditCard && account.CurrentBalance < 0)
        {
            throw new AppException($"Balance cannot go negative for {account.Name}.", 400);
        }
    }

    private static void EnsureSufficientSourceCapacity(Account account, decimal amount)
    {
        if (account.Type == AccountType.CreditCard)
        {
            var availableCredit = GetAvailableCredit(account) ?? 0;
            if (availableCredit < amount)
            {
                throw new AppException($"Limit exceeded for {account.Name}.");
            }

            return;
        }

        if (account.CurrentBalance < amount)
        {
            throw new AppException($"Insufficient funds in {account.Name}.");
        }
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
