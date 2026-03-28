using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Goals;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Application.Validators;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class GoalService(
    AppDbContext dbContext,
    IAccessControlService accessControlService,
    AccountActivityLogger activityLogger) : IGoalService
{
    private readonly GoalRequestValidator _validator = new();

    public async Task<IReadOnlyList<GoalResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        return await dbContext.Goals
            .Where(x =>
                x.UserId == userId ||
                (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value)))
            .Select(x => new GoalResponse(
                x.Id,
                x.Name,
                x.TargetAmount,
                x.CurrentAmount,
                x.TargetDate,
                x.LinkedAccountId,
                x.Icon,
                x.Color,
                x.Status,
                x.TargetAmount == 0 ? 0 : (x.CurrentAmount / x.TargetAmount) * 100))
            .ToListAsync(ct);
    }

    public async Task<GoalResponse> CreateAsync(Guid userId, GoalRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        if (request.LinkedAccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, request.LinkedAccountId.Value, ct);
        }

        var goal = new Goal
        {
            UserId = userId,
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            TargetDate = request.TargetDate,
            LinkedAccountId = request.LinkedAccountId,
            Icon = request.Icon,
            Color = request.Color,
            Status = "active"
        };

        dbContext.Goals.Add(goal);
        if (request.LinkedAccountId.HasValue)
        {
            activityLogger.Log(request.LinkedAccountId.Value, userId, "goal", "created", $"Created shared goal {goal.Name}.", goal.Id);
        }

        await dbContext.SaveChangesAsync(ct);
        return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId, goal.Icon, goal.Color, goal.Status, 0);
    }

    public async Task<GoalResponse> UpdateAsync(Guid userId, Guid id, GoalRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var goal = await GetGoalForEditAsync(userId, id, ct);

        if (request.LinkedAccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, request.LinkedAccountId.Value, ct);
        }

        goal.Name = request.Name;
        goal.TargetAmount = request.TargetAmount;
        goal.TargetDate = request.TargetDate;
        goal.LinkedAccountId = request.LinkedAccountId;
        goal.Icon = request.Icon;
        goal.Color = request.Color;
        if (goal.CurrentAmount >= goal.TargetAmount)
        {
            goal.Status = "completed";
        }
        else if (goal.Status == "completed")
        {
            goal.Status = "active";
        }

        if (goal.LinkedAccountId.HasValue)
        {
            activityLogger.Log(goal.LinkedAccountId.Value, userId, "goal", "updated", $"Updated goal {goal.Name}.", goal.Id);
        }

        await dbContext.SaveChangesAsync(ct);
        var progress = goal.TargetAmount == 0 ? 0 : (goal.CurrentAmount / goal.TargetAmount) * 100;
        return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId, goal.Icon, goal.Color, goal.Status, progress);
    }

    public async Task<GoalResponse> ContributeAsync(Guid userId, Guid id, decimal amount, Guid? accountId, CancellationToken ct = default)
    {
        if (amount <= 0) throw new AppException("Contribution must be greater than zero.");
        var goal = await GetGoalForEditAsync(userId, id, ct);

        if (goal.CurrentAmount >= goal.TargetAmount)
        {
            throw new AppException("Goal already achieved.");
        }

        var remainingAmount = goal.TargetAmount - goal.CurrentAmount;
        if (amount > remainingAmount)
        {
            throw new AppException($"Contribution exceeds remaining target amount ({remainingAmount:0.##}).");
        }

        var fundingAccount = await ResolveGoalActionAccountAsync(userId, goal, accountId, "contribution", ct);
        await CreateGoalTransactionAsync(userId, fundingAccount, goal, amount, TransactionType.Expense, ct);
        activityLogger.Log(fundingAccount.Id, userId, "goal", "contributed", $"Contributed {amount:0.##} to goal {goal.Name}.", goal.Id);

        goal.CurrentAmount += amount;
        if (goal.CurrentAmount >= goal.TargetAmount) goal.Status = "completed";

        await dbContext.SaveChangesAsync(ct);
        return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId, goal.Icon, goal.Color, goal.Status, (goal.CurrentAmount / goal.TargetAmount) * 100);
    }

    public async Task<GoalResponse> WithdrawAsync(Guid userId, Guid id, decimal amount, Guid? accountId, CancellationToken ct = default)
    {
        if (amount <= 0) throw new AppException("Withdraw amount must be greater than zero.");
        var goal = await GetGoalForEditAsync(userId, id, ct);

        if (goal.CurrentAmount < amount) throw new AppException("Insufficient goal amount.");
        goal.CurrentAmount -= amount;
        if (goal.Status == "completed" && goal.CurrentAmount < goal.TargetAmount) goal.Status = "active";

        var destinationAccount = await ResolveGoalActionAccountAsync(userId, goal, accountId, "withdrawal", ct);
        await CreateGoalTransactionAsync(userId, destinationAccount, goal, amount, TransactionType.Income, ct);
        activityLogger.Log(destinationAccount.Id, userId, "goal", "withdrawn", $"Withdrew {amount:0.##} from goal {goal.Name}.", goal.Id);

        await dbContext.SaveChangesAsync(ct);
        return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId, goal.Icon, goal.Color, goal.Status, (goal.CurrentAmount / goal.TargetAmount) * 100);
    }

    public async Task<GoalResponse> SetHoldStatusAsync(Guid userId, Guid id, bool onHold, CancellationToken ct = default)
    {
        var goal = await GetGoalForEditAsync(userId, id, ct);

        if (goal.Status == "completed")
        {
            throw new AppException("Completed goal cannot be put on hold.");
        }

        goal.Status = onHold ? "on-hold" : "active";
        if (goal.LinkedAccountId.HasValue)
        {
            activityLogger.Log(goal.LinkedAccountId.Value, userId, "goal", "hold-updated", $"Set goal {goal.Name} to {(onHold ? "on hold" : "active")}.", goal.Id);
        }

        await dbContext.SaveChangesAsync(ct);

        var progress = goal.TargetAmount == 0 ? 0 : (goal.CurrentAmount / goal.TargetAmount) * 100;
        return new GoalResponse(goal.Id, goal.Name, goal.TargetAmount, goal.CurrentAmount, goal.TargetDate, goal.LinkedAccountId, goal.Icon, goal.Color, goal.Status, progress);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var goal = await GetGoalForEditAsync(userId, id, ct);
        if (goal.LinkedAccountId.HasValue)
        {
            activityLogger.Log(goal.LinkedAccountId.Value, userId, "goal", "deleted", $"Deleted goal {goal.Name}.", goal.Id);
        }

        dbContext.Goals.Remove(goal);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<Goal> GetGoalForEditAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var goal = await dbContext.Goals.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("Goal not found.", 404);

        if (goal.LinkedAccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, goal.LinkedAccountId.Value, ct);
            return goal;
        }

        if (goal.UserId != userId)
        {
            throw new AppException("Goal not found.", 404);
        }

        return goal;
    }

    private async Task<Account> ResolveGoalActionAccountAsync(Guid userId, Goal goal, Guid? requestedAccountId, string actionLabel, CancellationToken ct)
    {
        var targetAccountId = goal.LinkedAccountId ?? requestedAccountId;
        if (!targetAccountId.HasValue)
        {
            throw new AppException($"Select an account for this goal {actionLabel}.", 400);
        }

        await accessControlService.EnsureCanEditAccountAsync(userId, targetAccountId.Value, ct);
        return await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == targetAccountId.Value, ct)
            ?? throw new AppException("Account not found.", 404);
    }

    private async Task CreateGoalTransactionAsync(
        Guid userId,
        Account account,
        Goal goal,
        decimal amount,
        TransactionType transactionType,
        CancellationToken ct)
    {
        var accountAccess = await accessControlService.GetAccountAccessAsync(userId, account.Id, ct);
        var categoryType = transactionType == TransactionType.Expense ? CategoryType.Expense : CategoryType.Income;
        var categoryName = transactionType == TransactionType.Expense ? "Goal Contribution" : "Goal Withdrawal";
        var category = await EnsureGoalCategoryAsync(accountAccess.OwnerUserId, categoryName, categoryType, ct);

        EnsureSufficientBalance(account, transactionType, amount);
        ApplyBalance(account, transactionType, amount);

        dbContext.Transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = account.Id,
            CategoryId = category.Id,
            Type = transactionType,
            Amount = amount,
            TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Merchant = goal.Name,
            Note = transactionType == TransactionType.Expense
                ? $"Goal contribution: {goal.Name}"
                : $"Goal withdrawal: {goal.Name}",
            PaymentMethod = "Goal"
        });

        account.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task<Category> EnsureGoalCategoryAsync(Guid ownerUserId, string name, CategoryType type, CancellationToken ct)
    {
        var existingCategory = await dbContext.Categories.FirstOrDefaultAsync(
            x => x.UserId == ownerUserId && x.Type == type && x.Name == name,
            ct);

        if (existingCategory is not null)
        {
            return existingCategory;
        }

        var category = new Category
        {
            UserId = ownerUserId,
            Name = name,
            Type = type,
            Color = type == CategoryType.Expense ? "#dd5757" : "#2ea05f",
            Icon = "target",
            IsArchived = false
        };

        dbContext.Categories.Add(category);
        return category;
    }

    private static void ApplyBalance(Account account, TransactionType type, decimal amount)
    {
        if (amount <= 0) throw new AppException("Amount must be greater than zero.");
        account.CurrentBalance += type == TransactionType.Income ? amount : -amount;
    }

    private static void EnsureSufficientBalance(Account account, TransactionType type, decimal amount)
    {
        if (type != TransactionType.Expense)
        {
            return;
        }

        if (account.Type == AccountType.CreditCard)
        {
            var availableCredit = (account.CreditLimit ?? 0) + account.CurrentBalance;
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
}
