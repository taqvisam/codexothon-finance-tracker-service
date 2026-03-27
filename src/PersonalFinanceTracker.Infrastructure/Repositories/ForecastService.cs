using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Forecast;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class ForecastService(AppDbContext dbContext, IAccessControlService accessControlService) : IForecastService
{
    public async Task<ForecastMonthResponse> GetMonthForecastAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var result = await BuildForecastResultAsync(userId, today, monthEnd, ct);

        return new ForecastMonthResponse(
            today.Year,
            today.Month,
            Math.Round(result.CurrentBalance, 2),
            Math.Round(result.ProjectedIncome, 2),
            Math.Round(result.ProjectedExpense, 2),
            Math.Round(result.UpcomingKnownExpenses, 2),
            Math.Round(result.ForecastedEndBalance, 2),
            Math.Round(result.SafeToSpend, 2),
            Math.Round(result.ConfidenceScore, 1),
            result.Model,
            result.EstimatedNegativeDate?.ToString("yyyy-MM-dd"),
            result.RiskWarnings);
    }

    public async Task<IReadOnlyList<DailyForecastPoint>> GetDailyForecastAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var result = await BuildForecastResultAsync(userId, today, monthEnd, ct);
        return result.DailyPoints;
    }

    private async Task<ForecastComputationResult> BuildForecastResultAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var currentBalance = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .SumAsync(x => x.CurrentBalance, ct);

        var (model, dailyProfile, historyCoverageDays) = await GetDailyProfileAsync(accessibleAccountIds, from, ct);
        var recurringDeltas = await GetRecurringDailyDeltasAsync(accessibleAccountIds, from, to, ct);

        var points = new List<DailyForecastPoint>();
        var running = currentBalance;
        decimal projectedIncome = 0m;
        decimal projectedExpense = 0m;
        decimal upcomingKnownExpenses = 0m;
        DateOnly? firstNegativeDate = null;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayType = IsWeekend(date) ? ForecastDayType.Weekend : ForecastDayType.Weekday;
            var daily = dailyProfile[dayType];
            var recurring = recurringDeltas.TryGetValue(date, out var delta)
                ? delta
                : new RecurringDayDelta(0m, 0m, 0m);

            var dayIncome = daily.Income + recurring.Income;
            var dayExpense = daily.Expense + recurring.Expense;
            running += dayIncome - dayExpense;

            projectedIncome += dayIncome;
            projectedExpense += dayExpense;
            upcomingKnownExpenses += recurring.Expense;

            if (running < 0m && firstNegativeDate is null)
            {
                firstNegativeDate = date;
            }

            points.Add(new DailyForecastPoint(date.ToString("yyyy-MM-dd"), Math.Round(running, 2)));
        }

        var forecastedEndBalance = points.LastOrDefault()?.ProjectedBalance ?? currentBalance;
        var reserve = Math.Max(projectedExpense * 0.10m, 0m);
        var safeToSpend = Math.Max(0m, forecastedEndBalance - reserve);
        var confidenceScore = ComputeConfidenceScore(historyCoverageDays, recurringDeltas.Count);
        var warnings = BuildWarnings(forecastedEndBalance, projectedExpense, currentBalance, firstNegativeDate, confidenceScore);

        return new ForecastComputationResult(
            currentBalance,
            projectedIncome,
            projectedExpense,
            upcomingKnownExpenses,
            forecastedEndBalance,
            safeToSpend,
            confidenceScore,
            model,
            firstNegativeDate,
            warnings,
            points);
    }

    private async Task<(string Model, Dictionary<ForecastDayType, DayAverage> Profile, int CoverageDays)> GetDailyProfileAsync(
        IReadOnlyList<Guid> accessibleAccountIds,
        DateOnly today,
        CancellationToken ct)
    {
        var lookBackStart = today.AddDays(-90);
        var history = await dbContext.Transactions
            .Where(x =>
                accessibleAccountIds.Contains(x.AccountId) &&
                x.TransactionDate >= lookBackStart &&
                x.TransactionDate < today &&
                x.Type != TransactionType.Transfer)
            .Select(x => new { x.TransactionDate, x.Type, x.Amount })
            .ToListAsync(ct);

        if (history.Count == 0)
        {
            var fallbackStart = today.AddDays(-30);
            var fallback = await dbContext.Transactions
                .Where(x =>
                    accessibleAccountIds.Contains(x.AccountId) &&
                    x.TransactionDate >= fallbackStart &&
                    x.TransactionDate < today &&
                    x.Type != TransactionType.Transfer)
                .Select(x => new { x.TransactionDate, x.Type, x.Amount })
                .ToListAsync(ct);

            var flatIncome = fallback.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount) / 30m;
            var flatExpense = fallback.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount) / 30m;
            return (
                "Sparse fallback average",
                new Dictionary<ForecastDayType, DayAverage>
                {
                    [ForecastDayType.Weekday] = new DayAverage(flatIncome, flatExpense),
                    [ForecastDayType.Weekend] = new DayAverage(flatIncome, flatExpense)
                },
                fallback.Select(x => x.TransactionDate).Distinct().Count());
        }

        var rows = history
            .Where(x =>
                x.TransactionDate >= lookBackStart &&
                x.TransactionDate < today)
            .ToList();

        DayAverage BuildAverage(ForecastDayType dayType)
        {
            var pool = rows.Where(x => (IsWeekend(x.TransactionDate) ? ForecastDayType.Weekend : ForecastDayType.Weekday) == dayType).ToList();
            if (pool.Count == 0)
            {
                pool = rows;
            }

            var grouped = pool
                .GroupBy(x => x.TransactionDate)
                .Select(group =>
                {
                    var distance = Math.Max(1, today.DayNumber - group.Key.DayNumber);
                    var recencyWeight = distance <= 30 ? 3m : distance <= 60 ? 2m : 1m;
                    var income = group.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                    var expense = group.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                    return new WeightedDay(income, expense, recencyWeight);
                })
                .ToList();

            var sumWeight = grouped.Sum(x => x.Weight);
            if (sumWeight <= 0m)
            {
                return new DayAverage(0m, 0m);
            }

            var incomeAverage = grouped.Sum(x => x.Income * x.Weight) / sumWeight;
            var expenseAverage = grouped.Sum(x => x.Expense * x.Weight) / sumWeight;
            return new DayAverage(incomeAverage, expenseAverage);
        }

        return (
            "Weighted recency weekday/weekend heuristic",
            new Dictionary<ForecastDayType, DayAverage>
            {
                [ForecastDayType.Weekday] = BuildAverage(ForecastDayType.Weekday),
                [ForecastDayType.Weekend] = BuildAverage(ForecastDayType.Weekend)
            },
            rows.Select(x => x.TransactionDate).Distinct().Count());
    }

    private async Task<Dictionary<DateOnly, RecurringDayDelta>> GetRecurringDailyDeltasAsync(
        IReadOnlyList<Guid> accessibleAccountIds,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var recurring = await dbContext.RecurringTransactions
            .Where(x =>
                x.AccountId.HasValue &&
                accessibleAccountIds.Contains(x.AccountId.Value) &&
                !x.IsPaused &&
                x.NextRunDate <= to &&
                (x.EndDate == null || x.EndDate >= from))
            .ToListAsync(ct);

        var deltas = new Dictionary<DateOnly, RecurringDayDelta>();

        foreach (var item in recurring)
        {
            var runDate = item.NextRunDate < from ? from : item.NextRunDate;
            while (runDate <= to)
            {
                var existing = deltas.TryGetValue(runDate, out var current)
                    ? current
                    : new RecurringDayDelta(0m, 0m, 0m);

                if (item.Type == TransactionType.Income)
                {
                    existing = existing with { Income = existing.Income + item.Amount };
                }
                else if (item.Type == TransactionType.Expense)
                {
                    existing = existing with
                    {
                        Expense = existing.Expense + item.Amount,
                        KnownExpenseOnly = existing.KnownExpenseOnly + item.Amount
                    };
                }

                deltas[runDate] = existing;
                runDate = GetNextRunDate(runDate, item.Frequency);
            }
        }

        return deltas;
    }

    private static DateOnly GetNextRunDate(DateOnly current, RecurringFrequency frequency)
        => frequency switch
        {
            RecurringFrequency.Daily => current.AddDays(1),
            RecurringFrequency.Weekly => current.AddDays(7),
            RecurringFrequency.Monthly => current.AddMonths(1),
            RecurringFrequency.Yearly => current.AddYears(1),
            _ => current.AddMonths(1)
        };

    private static IReadOnlyList<string> BuildWarnings(
        decimal forecastedEndBalance,
        decimal projectedExpense,
        decimal currentBalance,
        DateOnly? firstNegativeDate,
        decimal confidenceScore)
    {
        var warnings = new List<string>();
        if (forecastedEndBalance < 0)
        {
            warnings.Add("Negative balance likely by end of month.");
        }

        if (firstNegativeDate.HasValue)
        {
            warnings.Add($"Balance may turn negative on {firstNegativeDate:yyyy-MM-dd}.");
        }

        if (projectedExpense > currentBalance * 1.2m)
        {
            warnings.Add("Projected expenses are significantly higher than current balance.");
        }

        if (confidenceScore < 45m)
        {
            warnings.Add("Forecast confidence is limited due to sparse historical data.");
        }

        return warnings;
    }

    private static decimal ComputeConfidenceScore(int coverageDays, int recurringPointsCount)
    {
        var historyScore = Math.Min(70m, coverageDays * 0.9m);
        var recurringScore = Math.Min(30m, recurringPointsCount * 1.5m);
        return Math.Clamp(historyScore + recurringScore, 15m, 100m);
    }

    private static bool IsWeekend(DateOnly date)
    {
        var day = date.DayOfWeek;
        return day is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    private enum ForecastDayType
    {
        Weekday,
        Weekend
    }

    private readonly record struct DayAverage(decimal Income, decimal Expense);
    private readonly record struct RecurringDayDelta(decimal Income, decimal Expense, decimal KnownExpenseOnly);
    private readonly record struct WeightedDay(decimal Income, decimal Expense, decimal Weight);
    private readonly record struct ForecastComputationResult(
        decimal CurrentBalance,
        decimal ProjectedIncome,
        decimal ProjectedExpense,
        decimal UpcomingKnownExpenses,
        decimal ForecastedEndBalance,
        decimal SafeToSpend,
        decimal ConfidenceScore,
        string Model,
        DateOnly? EstimatedNegativeDate,
        IReadOnlyList<string> RiskWarnings,
        IReadOnlyList<DailyForecastPoint> DailyPoints);
}
