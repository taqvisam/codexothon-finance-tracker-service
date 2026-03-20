using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Reports;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class ReportService(AppDbContext dbContext) : IReportService
{
    public async Task<IReadOnlyList<CategorySpendReportItem>> GetCategorySpendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, TransactionType? type, CancellationToken ct = default)
    {
        var query = dbContext.Transactions
            .Where(x => x.UserId == userId && x.TransactionDate >= from && x.TransactionDate <= to);
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        else query = query.Where(x => x.Type == TransactionType.Expense);

        var transactions = await query
            .Select(t => new { t.CategoryId, t.Amount })
            .ToListAsync(ct);

        var categories = await dbContext.Categories
            .Where(c => c.UserId == userId)
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return transactions
            .GroupBy(t =>
            {
                if (t.CategoryId.HasValue && categories.TryGetValue(t.CategoryId.Value, out var name))
                {
                    return name;
                }

                return "Uncategorized";
            })
            .Select(g => new CategorySpendReportItem(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    public async Task<IReadOnlyList<IncomeExpenseReportItem>> GetIncomeVsExpenseAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, TransactionType? type, CancellationToken ct = default)
    {
        var query = dbContext.Transactions
            .Where(x => x.UserId == userId && x.TransactionDate >= from && x.TransactionDate <= to);
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        var transactions = await query.ToListAsync(ct);

        return transactions
            .GroupBy(x => $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}")
            .OrderBy(x => x.Key)
            .Select(g => new IncomeExpenseReportItem(
                g.Key,
                g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount)))
            .ToList();
    }

    public async Task<IReadOnlyList<AccountBalanceTrendItem>> GetAccountBalanceTrendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, TransactionType? type, CancellationToken ct = default)
    {
        var accounts = await dbContext.Accounts
            .Where(x => x.UserId == userId && (!accountId.HasValue || x.Id == accountId.Value))
            .ToListAsync(ct);
        if (accounts.Count == 0) return new List<AccountBalanceTrendItem>();

        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var txQuery = dbContext.Transactions
            .Where(x => x.UserId == userId && accountIds.Contains(x.AccountId) && x.TransactionDate <= to);
        if (categoryId.HasValue) txQuery = txQuery.Where(x => x.CategoryId == categoryId.Value);
        if (type.HasValue) txQuery = txQuery.Where(x => x.Type == type.Value);

        var transactions = await txQuery
            .OrderBy(x => x.TransactionDate)
            .Select(x => new { x.AccountId, x.TransactionDate, x.Type, x.Amount })
            .ToListAsync(ct);

        var result = new List<AccountBalanceTrendItem>();
        foreach (var account in accounts)
        {
            var accountTx = transactions.Where(x => x.AccountId == account.Id).ToList();
            var historicalDelta = accountTx
                .Where(t => t.TransactionDate < from)
                .Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount);
            decimal runningBalance = account.OpeningBalance + historicalDelta;

            // Build a simple trend over transaction dates within requested window.
            foreach (var dateGroup in accountTx.Where(t => t.TransactionDate >= from).GroupBy(t => t.TransactionDate).OrderBy(g => g.Key))
            {
                foreach (var t in dateGroup)
                {
                    runningBalance += t.Type == TransactionType.Income ? t.Amount : -t.Amount;
                }
                result.Add(new AccountBalanceTrendItem(dateGroup.Key.ToString("yyyy-MM-dd"), account.Name, runningBalance));
            }

            // Ensure the chart always has a current point for the account.
            if (!result.Any(r => r.AccountName == account.Name))
            {
                result.Add(new AccountBalanceTrendItem(to.ToString("yyyy-MM-dd"), account.Name, account.CurrentBalance));
            }
        }

        return result
            .OrderBy(x => x.Date)
            .ThenBy(x => x.AccountName)
            .ToList();
    }
}
