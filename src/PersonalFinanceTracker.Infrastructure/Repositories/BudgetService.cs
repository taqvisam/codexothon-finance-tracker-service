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

public class BudgetService(AppDbContext dbContext) : IBudgetService
{
    private readonly BudgetRequestValidator _validator = new();

    public async Task<IReadOnlyList<BudgetResponse>> GetAllAsync(Guid userId, int? month, int? year, CancellationToken ct = default)
    {
        var query = dbContext.Budgets.Where(x => x.UserId == userId);
        if (month.HasValue) query = query.Where(x => x.Month == month);
        if (year.HasValue) query = query.Where(x => x.Year == year);

        var budgets = await query.ToListAsync(ct);
        var results = new List<BudgetResponse>();

        foreach (var budget in budgets)
        {
            var spent = await dbContext.Transactions
                .Where(x => x.UserId == userId && x.CategoryId == budget.CategoryId && x.Type == TransactionType.Expense && x.TransactionDate.Month == budget.Month && x.TransactionDate.Year == budget.Year)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;

            results.Add(new BudgetResponse(budget.Id, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, spent));
        }

        return results;
    }

    public async Task<BudgetResponse> CreateAsync(Guid userId, BudgetRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        await EnsureBudgetCategoryIsValidAsync(userId, request.CategoryId, ct);

        var exists = await dbContext.Budgets.AnyAsync(x => x.UserId == userId && x.CategoryId == request.CategoryId && x.Month == request.Month && x.Year == request.Year, ct);
        if (exists) throw new AppException("Budget already exists for category/month.", 409);

        var budget = new Budget
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Month = request.Month,
            Year = request.Year,
            Amount = request.Amount,
            AlertThresholdPercent = request.AlertThresholdPercent
        };

        dbContext.Budgets.Add(budget);
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new AppException("Budget already exists for category/month.", 409);
        }
        return new BudgetResponse(budget.Id, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, 0);
    }

    public async Task<BudgetResponse> UpdateAsync(Guid userId, Guid id, BudgetRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        await EnsureBudgetCategoryIsValidAsync(userId, request.CategoryId, ct);
        var budget = await dbContext.Budgets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Budget not found.", 404);

        budget.CategoryId = request.CategoryId;
        budget.Month = request.Month;
        budget.Year = request.Year;
        budget.Amount = request.Amount;
        budget.AlertThresholdPercent = request.AlertThresholdPercent;
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            throw new AppException("Budget already exists for category/month.", 409);
        }

        return new BudgetResponse(budget.Id, budget.CategoryId, budget.Month, budget.Year, budget.Amount, budget.AlertThresholdPercent, 0);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var budget = await dbContext.Budgets.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Budget not found.", 404);
        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicateLastMonthAsync(Guid userId, int month, int year, CancellationToken ct = default)
    {
        var prev = new DateOnly(year, month, 1).AddMonths(-1);
        var previousBudgets = await dbContext.Budgets
            .Where(x => x.UserId == userId && x.Month == prev.Month && x.Year == prev.Year)
            .ToListAsync(ct);
        var created = 0;
        foreach (var b in previousBudgets)
        {
            var exists = await dbContext.Budgets.AnyAsync(
                x => x.UserId == userId && x.CategoryId == b.CategoryId && x.Month == month && x.Year == year, ct);
            if (exists) continue;
            dbContext.Budgets.Add(new Budget
            {
                Id = Guid.NewGuid(),
                UserId = userId,
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

    private async Task EnsureBudgetCategoryIsValidAsync(Guid userId, Guid categoryId, CancellationToken ct)
    {
        var category = await dbContext.Categories
            .Where(x => x.Id == categoryId && x.UserId == userId)
            .Select(x => new { x.Id, x.Type })
            .FirstOrDefaultAsync(ct);

        if (category is null)
        {
            throw new AppException("Category not found for current user.", 404);
        }

        if (category.Type != CategoryType.Expense)
        {
            throw new AppException("Budgets can only be created for expense categories.", 400);
        }
    }
}
