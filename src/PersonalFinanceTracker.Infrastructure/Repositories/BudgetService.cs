using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersonalFinanceTracker.Application.DTOs.Budgets;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Application.Validators;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class BudgetService(
    AppDbContext dbContext,
    IAccessControlService accessControlService,
    AccountActivityLogger activityLogger) : IBudgetService
{
    private readonly BudgetRequestValidator _validator = new();

    public async Task<IReadOnlyList<BudgetResponse>> GetAllAsync(Guid userId, int? month, int? year, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var query = dbContext.Budgets.Where(x =>
            (x.AccountId == null && x.UserId == userId) ||
            (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value)));
        if (month.HasValue) query = query.Where(x => x.Month == month);
        if (year.HasValue) query = query.Where(x => x.Year == year);

        var budgets = await query.ToListAsync(ct);
        var results = new List<BudgetResponse>();

        foreach (var budget in budgets)
        {
            var spentQuery = dbContext.Transactions
                .Where(x =>
                    x.CategoryId == budget.CategoryId &&
                    x.Type == TransactionType.Expense &&
                    x.TransactionDate.Month == budget.Month &&
                    x.TransactionDate.Year == budget.Year);

            if (budget.AccountId.HasValue)
            {
                spentQuery = spentQuery.Where(x => x.AccountId == budget.AccountId.Value);
            }
            else
            {
                spentQuery = spentQuery.Where(x => x.UserId == userId);
            }

            var spent = await spentQuery.SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            results.Add(new BudgetResponse(budget.Id, budget.AccountId, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, spent));
        }

        return results;
    }

    public async Task<BudgetResponse> CreateAsync(Guid userId, BudgetRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var ownerUserId = await ResolveBudgetOwnerUserIdAsync(userId, request.AccountId, ct);
        await EnsureBudgetCategoryIsValidAsync(ownerUserId, request.CategoryId, ct);

        var exists = await dbContext.Budgets.AnyAsync(x =>
            x.UserId == ownerUserId &&
            x.AccountId == request.AccountId &&
            x.CategoryId == request.CategoryId &&
            x.Month == request.Month &&
            x.Year == request.Year, ct);
        if (exists) throw new AppException("Budget already exists for category/month.", 409);

        var budget = new Budget
        {
            UserId = ownerUserId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Month = request.Month,
            Year = request.Year,
            Amount = request.Amount,
            AlertThresholdPercent = request.AlertThresholdPercent
        };

        dbContext.Budgets.Add(budget);
        if (request.AccountId.HasValue)
        {
            activityLogger.Log(request.AccountId.Value, userId, "budget", "created", $"Created shared budget for {request.Month}/{request.Year}.", budget.Id);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new AppException("Budget already exists for category/month.", 409);
        }

        return new BudgetResponse(budget.Id, budget.AccountId, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, 0);
    }

    public async Task<BudgetResponse> UpdateAsync(Guid userId, Guid id, BudgetRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        var budget = await GetBudgetForAccessAsync(userId, id, ct);
        var ownerUserId = await ResolveBudgetOwnerUserIdAsync(userId, request.AccountId, ct);
        await EnsureBudgetCategoryIsValidAsync(ownerUserId, request.CategoryId, ct);

        budget.UserId = ownerUserId;
        budget.AccountId = request.AccountId;
        budget.CategoryId = request.CategoryId;
        budget.Month = request.Month;
        budget.Year = request.Year;
        budget.Amount = request.Amount;
        budget.AlertThresholdPercent = request.AlertThresholdPercent;

        if (request.AccountId.HasValue)
        {
            activityLogger.Log(request.AccountId.Value, userId, "budget", "updated", $"Updated shared budget for {request.Month}/{request.Year}.", budget.Id);
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new AppException("Budget already exists for category/month.", 409);
        }

        return new BudgetResponse(budget.Id, budget.AccountId, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, 0);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var budget = await GetBudgetForAccessAsync(userId, id, ct);
        if (budget.AccountId.HasValue)
        {
            activityLogger.Log(budget.AccountId.Value, userId, "budget", "deleted", "Deleted shared budget.", budget.Id);
        }

        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateLastMonthAsync(Guid userId, int month, int year, CancellationToken ct = default)
    {
        var prev = new DateOnly(year, month, 1).AddMonths(-1);
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var previousBudgets = await dbContext.Budgets
            .Where(x =>
                x.Month == prev.Month &&
                x.Year == prev.Year &&
                ((x.AccountId == null && x.UserId == userId) ||
                 (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))))
            .ToListAsync(ct);

        var created = 0;
        foreach (var b in previousBudgets)
        {
            if (b.AccountId.HasValue)
            {
                await accessControlService.EnsureCanEditAccountAsync(userId, b.AccountId.Value, ct);
            }

            var exists = await dbContext.Budgets.AnyAsync(
                x => x.UserId == b.UserId && x.AccountId == b.AccountId && x.CategoryId == b.CategoryId && x.Month == month && x.Year == year, ct);
            if (exists) continue;

            dbContext.Budgets.Add(new Budget
            {
                Id = Guid.NewGuid(),
                UserId = b.UserId,
                AccountId = b.AccountId,
                CategoryId = b.CategoryId,
                Month = month,
                Year = year,
                Amount = b.Amount,
                AlertThresholdPercent = b.AlertThresholdPercent
            });
            created++;
        }

        await dbContext.SaveChangesAsync(ct);
        return created;
    }

    private async Task<Budget> GetBudgetForAccessAsync(Guid userId, Guid budgetId, CancellationToken ct)
    {
        var budget = await dbContext.Budgets.FirstOrDefaultAsync(x => x.Id == budgetId, ct)
            ?? throw new AppException("Budget not found.", 404);

        if (budget.AccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, budget.AccountId.Value, ct);
        }
        else if (budget.UserId != userId)
        {
            throw new AppException("Budget not found.", 404);
        }

        return budget;
    }

    private async Task<Guid> ResolveBudgetOwnerUserIdAsync(Guid userId, Guid? accountId, CancellationToken ct)
    {
        if (!accountId.HasValue)
        {
            return userId;
        }

        var access = await accessControlService.EnsureCanEditAccountAsync(userId, accountId.Value, ct);
        return access.OwnerUserId;
    }

    private async Task EnsureBudgetCategoryIsValidAsync(Guid categoryOwnerId, Guid categoryId, CancellationToken ct)
    {
        var category = await dbContext.Categories
            .Where(x => x.Id == categoryId && x.UserId == categoryOwnerId)
            .Select(x => new { x.Id, x.Type })
            .FirstOrDefaultAsync(ct);

        if (category is null)
        {
            throw new AppException("Category not found for current budget scope.", 404);
        }

        if (category.Type != CategoryType.Expense)
        {
            throw new AppException("Budgets can only be created for expense categories.", 400);
        }
    }
}
