using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Reports;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class ReportService(AppDbContext dbContext, IAccessControlService accessControlService) : IReportService
{
    public async Task<IReadOnlyList<CategorySpendReportItem>> GetCategorySpendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, TransactionType? type, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var transactions = await BuildScopedTransactionQuery(accessibleAccountIds, from, to, accountId, categoryId, type)
            .Where(x => type.HasValue || x.Type == TransactionType.Expense)
            .Select(t => new { t.CategoryId, t.Amount })
            .ToListAsync(ct);

        var ownerIds = await accessControlService.GetAccessibleAccountOwnerIdsAsync(userId, ct);
        var categories = await dbContext.Categories
            .Where(c => ownerIds.Contains(c.UserId))
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
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var transactions = await BuildScopedTransactionQuery(accessibleAccountIds, from, to, accountId, categoryId, type).ToListAsync(ct);

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
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var accounts = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id) && (!accountId.HasValue || x.Id == accountId.Value))
            .ToListAsync(ct);
        if (accounts.Count == 0) return new List<AccountBalanceTrendItem>();

        var accountIds = accounts.Select(a => a.Id).ToHashSet();
        var txQuery = dbContext.Transactions
            .Where(x => accountIds.Contains(x.AccountId) && x.TransactionDate <= to);
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

            foreach (var dateGroup in accountTx.Where(t => t.TransactionDate >= from).GroupBy(t => t.TransactionDate).OrderBy(g => g.Key))
            {
                foreach (var t in dateGroup)
                {
                    runningBalance += t.Type == TransactionType.Income ? t.Amount : -t.Amount;
                }
                result.Add(new AccountBalanceTrendItem(dateGroup.Key.ToString("yyyy-MM-dd"), account.Name, runningBalance));
            }

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

    public async Task<IReadOnlyList<CategoryTrendReportItem>> GetCategoryTrendsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, CancellationToken ct = default)
    {
        var ownerIds = await accessControlService.GetAccessibleAccountOwnerIdsAsync(userId, ct);
        var categories = await dbContext.Categories
            .Where(c => ownerIds.Contains(c.UserId))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var rows = await BuildScopedTransactionQuery(accessibleAccountIds, from, to, accountId, categoryId, TransactionType.Expense)
            .Select(x => new { x.TransactionDate, x.CategoryId, x.Amount })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => new
            {
                Period = $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}",
                Category = x.CategoryId.HasValue && categories.TryGetValue(x.CategoryId.Value, out var resolvedName) ? resolvedName : "Uncategorized"
            })
            .OrderBy(x => x.Key.Period)
            .ThenBy(x => x.Key.Category)
            .Select(g => new CategoryTrendReportItem(g.Key.Period, g.Key.Category, g.Sum(x => x.Amount)))
            .ToList();
    }

    public async Task<IReadOnlyList<SavingsRateTrendReportItem>> GetSavingsRateTrendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var transactions = await BuildScopedTransactionQuery(accessibleAccountIds, from, to, accountId, null, null).ToListAsync(ct);

        return transactions
            .GroupBy(x => $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}")
            .OrderBy(x => x.Key)
            .Select(g =>
            {
                var income = g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                var expense = g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                var savingsRate = income <= 0 ? 0 : ((income - expense) / income) * 100m;
                return new SavingsRateTrendReportItem(g.Key, Math.Round(savingsRate, 2));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<NetWorthReportItem>> GetNetWorthAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var accounts = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id) && (!accountId.HasValue || x.Id == accountId.Value))
            .ToListAsync(ct);

        var periods = Enumerable.Range(0, ((to.Year - from.Year) * 12) + to.Month - from.Month + 1)
            .Select(offset => new DateOnly(from.Year, from.Month, 1).AddMonths(offset))
            .ToList();

        var transactions = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.TransactionDate <= to && (!accountId.HasValue || x.AccountId == accountId.Value))
            .Select(x => new { x.AccountId, x.TransactionDate, x.Type, x.Amount })
            .ToListAsync(ct);

        var result = new List<NetWorthReportItem>();
        foreach (var period in periods)
        {
            var periodEnd = new DateOnly(period.Year, period.Month, DateTime.DaysInMonth(period.Year, period.Month));
            decimal netWorth = 0;
            foreach (var account in accounts)
            {
                var delta = transactions
                    .Where(x => x.AccountId == account.Id && x.TransactionDate <= periodEnd)
                    .Sum(x => x.Type == TransactionType.Income ? x.Amount : -x.Amount);
                netWorth += account.OpeningBalance + delta;
            }

            result.Add(new NetWorthReportItem($"{period.Year}-{period.Month:00}", Math.Round(netWorth, 2)));
        }

        return result;
    }

    private IQueryable<Domain.Entities.Transaction> BuildScopedTransactionQuery(
        IReadOnlyList<Guid> accessibleAccountIds,
        DateOnly from,
        DateOnly to,
        Guid? accountId,
        Guid? categoryId,
        TransactionType? type)
    {
        var query = dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.TransactionDate >= from && x.TransactionDate <= to);
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        return query;
    }
}
