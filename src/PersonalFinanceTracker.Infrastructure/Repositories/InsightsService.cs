using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Insights;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class InsightsService(
    AppDbContext dbContext,
    IAccessControlService accessControlService,
    IReportService reportService) : IInsightsService
{
    public async Task<HealthScoreResponse> GetHealthScoreAsync(Guid userId, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var lookbackStart = monthStart.AddMonths(-6);

        var transactions = await dbContext.Transactions
            .Where(x =>
                accessibleAccountIds.Contains(x.AccountId) &&
                x.TransactionDate >= lookbackStart &&
                x.TransactionDate <= today &&
                x.Type != TransactionType.Transfer)
            .ToListAsync(ct);

        var currentMonthTx = transactions.Where(x => x.TransactionDate >= monthStart).ToList();
        var currentIncome = currentMonthTx.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var currentExpense = currentMonthTx.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

        var savingsRateScore = ComputeSavingsRateScore(currentIncome, currentExpense);
        var expenseStabilityScore = ComputeExpenseStabilityScore(transactions);
        var budgetAdherenceScore = await ComputeBudgetAdherenceScoreAsync(userId, accessibleAccountIds, today.Year, today.Month, ct);
        var cashBufferScore = await ComputeCashBufferScoreAsync(accessibleAccountIds, transactions, ct);

        var weightedScore =
            (savingsRateScore * 0.30m) +
            (expenseStabilityScore * 0.20m) +
            (budgetAdherenceScore * 0.25m) +
            (cashBufferScore * 0.25m);
        var finalScore = Math.Round(Math.Clamp(weightedScore, 0m, 100m), 1);

        var breakdown = new List<HealthScoreFactor>
        {
            new("Savings rate", Math.Round(savingsRateScore, 1), "How much of income is left after expenses."),
            new("Expense stability", Math.Round(expenseStabilityScore, 1), "Consistency of month-over-month spending."),
            new("Budget adherence", Math.Round(budgetAdherenceScore, 1), "How well your spending stays within budgets."),
            new("Cash buffer", Math.Round(cashBufferScore, 1), "How many months of expenses your balances can cover.")
        };

        var suggestions = BuildSuggestions(finalScore, savingsRateScore, expenseStabilityScore, budgetAdherenceScore, cashBufferScore);
        return new HealthScoreResponse(finalScore, breakdown, suggestions);
    }

    public async Task<IReadOnlyList<InsightHighlightResponse>> GetHighlightsAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, CancellationToken ct = default)
    {
        var previousFrom = from.AddMonths(-1);
        var previousTo = to.AddMonths(-1);

        var currentCategorySpend = await reportService.GetCategorySpendAsync(userId, from, to, accountId, categoryId, TransactionType.Expense, ct);
        var previousCategorySpend = await reportService.GetCategorySpendAsync(userId, previousFrom, previousTo, accountId, categoryId, TransactionType.Expense, ct);
        var savingsTrend = await reportService.GetSavingsRateTrendAsync(userId, previousFrom, to, accountId, ct);
        var incomeExpense = await reportService.GetIncomeVsExpenseAsync(userId, previousFrom, to, accountId, categoryId, null, ct);

        var highlights = new List<InsightHighlightResponse>();

        var topCurrent = currentCategorySpend.FirstOrDefault();
        if (topCurrent is not null)
        {
            var previousAmount = previousCategorySpend.FirstOrDefault(x => x.Category == topCurrent.Category)?.Amount ?? 0m;
            var changePercent = ComputeChangePercent(previousAmount, topCurrent.Amount);
            var severity = changePercent >= 15 ? "warning" : "info";
            highlights.Add(new InsightHighlightResponse(
                "Category Trend",
                $"Your {topCurrent.Category} spending changed {Math.Round(changePercent, 1)}% compared to the previous period.",
                severity,
                Math.Round(changePercent, 1),
                $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}"));
        }

        if (savingsTrend.Count >= 2)
        {
            var latest = savingsTrend[^1];
            var previous = savingsTrend[^2];
            var delta = latest.SavingsRate - previous.SavingsRate;
            highlights.Add(new InsightHighlightResponse(
                "Savings Rate",
                delta >= 0
                    ? $"You saved more than last month by {Math.Round(delta, 1)} percentage points."
                    : $"Your savings rate dropped by {Math.Round(Math.Abs(delta), 1)} percentage points.",
                delta >= 0 ? "success" : "warning",
                Math.Round(delta, 1),
                latest.Period));
        }

        if (incomeExpense.Count >= 2)
        {
            var latest = incomeExpense[^1];
            var previous = incomeExpense[^2];
            var expenseDelta = ComputeChangePercent(previous.Expense, latest.Expense);
            highlights.Add(new InsightHighlightResponse(
                "Expense Momentum",
                $"Expenses moved {Math.Round(expenseDelta, 1)}% versus the previous month.",
                expenseDelta > 0 ? "warning" : "success",
                Math.Round(expenseDelta, 1),
                latest.Month));
        }

        return highlights;
    }

    private static decimal ComputeSavingsRateScore(decimal income, decimal expense)
    {
        if (income <= 0m)
        {
            return expense <= 0m ? 50m : 20m;
        }

        var savingsRate = (income - expense) / income;
        return Math.Clamp(savingsRate * 100m, 0m, 100m);
    }

    private static decimal ComputeExpenseStabilityScore(IReadOnlyList<Domain.Entities.Transaction> transactions)
    {
        var monthlyExpenses = transactions
            .Where(x => x.Type == TransactionType.Expense)
            .GroupBy(x => $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}")
            .Select(g => g.Sum(x => x.Amount))
            .ToList();

        if (monthlyExpenses.Count <= 1)
        {
            return 60m;
        }

        var mean = monthlyExpenses.Average();
        if (mean <= 0m)
        {
            return 100m;
        }

        var variance = monthlyExpenses.Average(x => (double)((x - mean) * (x - mean)));
        var stdDev = (decimal)Math.Sqrt(variance);
        var coeffVariance = stdDev / mean;
        var score = 100m - (coeffVariance * 100m);
        return Math.Clamp(score, 0m, 100m);
    }

    private async Task<decimal> ComputeBudgetAdherenceScoreAsync(Guid userId, IReadOnlyList<Guid> accessibleAccountIds, int year, int month, CancellationToken ct)
    {
        var budgets = await dbContext.Budgets
            .Where(x =>
                (x.AccountId == null && x.UserId == userId && x.Year == year && x.Month == month) ||
                (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value) && x.Year == year && x.Month == month))
            .ToListAsync(ct);

        if (budgets.Count == 0)
        {
            return 60m;
        }

        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var expenses = await dbContext.Transactions
            .Where(x =>
                accessibleAccountIds.Contains(x.AccountId) &&
                x.Type == TransactionType.Expense &&
                x.TransactionDate >= monthStart &&
                x.TransactionDate <= monthEnd &&
                x.CategoryId != null)
            .ToListAsync(ct);

        decimal scoreTotal = 0m;
        foreach (var budget in budgets)
        {
            var spent = expenses
                .Where(x => x.CategoryId == budget.CategoryId && (!budget.AccountId.HasValue || x.AccountId == budget.AccountId.Value))
                .Sum(x => x.Amount);

            if (budget.Amount <= 0m)
            {
                scoreTotal += 50m;
                continue;
            }

            var usage = spent / budget.Amount;
            var score = usage <= 1m
                ? 100m
                : Math.Max(0m, 100m - ((usage - 1m) * 100m));
            scoreTotal += score;
        }

        return Math.Clamp(scoreTotal / budgets.Count, 0m, 100m);
    }

    private async Task<decimal> ComputeCashBufferScoreAsync(
        IReadOnlyList<Guid> accessibleAccountIds,
        IReadOnlyList<Domain.Entities.Transaction> transactions,
        CancellationToken ct)
    {
        var currentBalance = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .SumAsync(x => x.CurrentBalance, ct);

        var monthlyExpenses = transactions
            .Where(x => x.Type == TransactionType.Expense)
            .GroupBy(x => $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}")
            .Select(g => g.Sum(x => x.Amount))
            .ToList();

        var averageExpense = monthlyExpenses.Count == 0 ? 0m : monthlyExpenses.Average();
        if (averageExpense <= 0m)
        {
            return currentBalance > 0m ? 100m : 50m;
        }

        var monthsCovered = currentBalance / averageExpense;
        return Math.Clamp((monthsCovered / 3m) * 100m, 0m, 100m);
    }

    private static IReadOnlyList<string> BuildSuggestions(
        decimal score,
        decimal savingsRateScore,
        decimal expenseStabilityScore,
        decimal budgetAdherenceScore,
        decimal cashBufferScore)
    {
        var suggestions = new List<string>();
        if (savingsRateScore < 50m)
        {
            suggestions.Add("Try increasing savings rate by reducing discretionary spending.");
        }

        if (expenseStabilityScore < 55m)
        {
            suggestions.Add("Expenses fluctuate heavily; set spending limits for variable categories.");
        }

        if (budgetAdherenceScore < 60m)
        {
            suggestions.Add("Budget overruns detected; adjust budget caps or spending behavior.");
        }

        if (cashBufferScore < 50m)
        {
            suggestions.Add("Build a larger cash buffer to cover at least 2-3 months of expenses.");
        }

        if (suggestions.Count == 0 && score >= 80m)
        {
            suggestions.Add("Great progress. Maintain your savings and budgeting habits.");
        }

        return suggestions;
    }

    private static decimal ComputeChangePercent(decimal previousValue, decimal currentValue)
    {
        if (previousValue == 0)
        {
            return currentValue == 0 ? 0 : 100;
        }

        return ((currentValue - previousValue) / previousValue) * 100m;
    }
}
