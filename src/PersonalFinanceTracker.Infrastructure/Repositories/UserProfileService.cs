using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class UserProfileService(AppDbContext dbContext) : IUserProfileService
{
    public async Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        return new UserProfileResponse(
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.ProfileImageUrl
        );
    }

    public async Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new AppException("Email is required.");
        }

        var duplicateEmailExists = await dbContext.Users.AnyAsync(
            x => x.Id != userId && x.Email == normalizedEmail,
            ct
        );
        if (duplicateEmailExists)
        {
            throw new AppException("Email already exists.", 409);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.DisplayName : request.DisplayName.Trim();
        if (displayName.Length < 2)
        {
            throw new AppException("Display name must be at least 2 characters.");
        }

        var profileImage = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        if (profileImage is not null && profileImage.Length > 3_000_000)
        {
            throw new AppException("Profile image is too large.");
        }

        user.DisplayName = displayName;
        user.Email = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.ProfileImageUrl = profileImage;

        await dbContext.SaveChangesAsync(ct);

        return new UserProfileResponse(
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.ProfileImageUrl
        );
    }

    public async Task DeleteAsync(Guid userId, DeleteUserAccountRequest request, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        if (!request.DeleteData)
        {
            user.IsSoftDeleted = true;
            user.SoftDeletedAt = DateTime.UtcNow;
            user.ShowWelcomeBackMessage = false;
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var ownedAccountIds = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedBudgetIds = await dbContext.Budgets
            .Where(x => x.UserId == userId || (x.AccountId.HasValue && ownedAccountIds.Contains(x.AccountId.Value)))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedGoalIds = await dbContext.Goals
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedTransactionIds = await dbContext.Transactions
            .Where(x => x.UserId == userId || ownedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && ownedAccountIds.Contains(x.TransferAccountId.Value)))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedRecurringIds = await dbContext.RecurringTransactions
            .Where(x => x.UserId == userId || (x.AccountId.HasValue && ownedAccountIds.Contains(x.AccountId.Value)))
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedActivityIds = await dbContext.AccountActivities
            .Where(x => ownedAccountIds.Contains(x.AccountId) || x.ActorUserId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var linkedMembershipIds = await dbContext.AccountMembers
            .Where(x => x.UserId == userId || ownedAccountIds.Contains(x.AccountId))
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (linkedActivityIds.Count > 0)
        {
            await dbContext.AccountActivities.Where(x => linkedActivityIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        if (linkedMembershipIds.Count > 0)
        {
            await dbContext.AccountMembers.Where(x => linkedMembershipIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        if (linkedRecurringIds.Count > 0)
        {
            await dbContext.RecurringTransactions.Where(x => linkedRecurringIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        if (linkedBudgetIds.Count > 0)
        {
            await dbContext.Budgets.Where(x => linkedBudgetIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        if (linkedGoalIds.Count > 0)
        {
            await dbContext.Goals.Where(x => linkedGoalIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        if (linkedTransactionIds.Count > 0)
        {
            await dbContext.Transactions.Where(x => linkedTransactionIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        await dbContext.Rules.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
        await dbContext.Categories.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

        if (ownedAccountIds.Count > 0)
        {
            await dbContext.Accounts.Where(x => ownedAccountIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(ct);
    }
}
