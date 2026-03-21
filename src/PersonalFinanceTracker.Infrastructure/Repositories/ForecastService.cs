using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Forecast;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class ForecastService(AppDbContext dbContext) : IForecastService
{
    public async Task<ForecastMonthResponse> GetMonthForecastAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        var currentBalance = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.CurrentBalance, ct);

        var (avgMonthlyIncome, avgMonthlyExpense) = await GetHistoricalMonthlyAveragesAsync(userId, monthStart, ct);
        var (knownIncome, knownExpense) = await GetKnownRecurringTotalsAsync(userId, today, monthEnd, ct);

        var projectedIncome = Math.Max(avgMonthlyIncome, 0m) + knownIncome;
        var projectedExpense = Math.Max(avgMonthlyExpense, 0m) + knownExpense;
        var forecastedEndBalance = currentBalance + projectedIncome - projectedExpense;
        var safeToSpend = Math.Max(0m, forecastedEndBalance);

        var warnings = BuildWarnings(forecastedEndBalance, projectedExpense, currentBalance);

        return new ForecastMonthResponse(
            today.Year,
            today.Month,
            Math.Round(currentBalance, 2),
            Math.Round(projectedIncome, 2),
            Math.Round(projectedExpense, 2),
            Math.Round(knownExpense, 2),
            Math.Round(forecastedEndBalance, 2),
            Math.Round(safeToSpend, 2),
            warnings);
    }

    public async Task<IReadOnlyList<DailyForecastPoint>> GetDailyForecastAsync(Guid userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

        var currentBalance = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .SumAsync(x => x.CurrentBalance, ct);

        var (avgMonthlyIncome, avgMonthlyExpense) = await GetHistoricalMonthlyAveragesAsync(
            userId,
            new DateOnly(today.Year, today.Month, 1),
            ct);

        var remainingDays = Math.Max(1, monthEnd.DayNumber - today.DayNumber + 1);
        var dailyIncome = avgMonthlyIncome / remainingDays;
        var dailyExpense = avgMonthlyExpense / remainingDays;

        var recurrences = await dbContext.RecurringTransactions
            .Where(x =>
                x.UserId == userId &&
                !x.IsPaused &&
                x.NextRunDate <= monthEnd &&
                (x.EndDate == null || x.EndDate >= today))
            .ToListAsync(ct);

        var recurringByDate = new Dictionary<DateOnly, decimal>();
        foreach (var recurring in recurrences)
        {
            var runDate = recurring.NextRunDate < today ? today : recurring.NextRunDate;
            while (runDate <= monthEnd)
            {
                var delta = recurring.Type == TransactionType.Income ? recurring.Amount : -recurring.Amount;
                recurringByDate[runDate] = recurringByDate.TryGetValue(runDate, out var existing)
                    ? existing + delta
                    : delta;
                runDate = GetNextRunDate(runDate, recurring.Frequency);
            }
        }

        var points = new List<DailyForecastPoint>();
        var balance = currentBalance;
        for (var date = today; date <= monthEnd; date = date.AddDays(1))
        {
            balance += dailyIncome;
            balance -= dailyExpense;

            if (recurringByDate.TryGetValue(date, out var recurringDelta))
            {
                balance += recurringDelta;
            }

            points.Add(new DailyForecastPoint(date.ToString("yyyy-MM-dd"), Math.Round(balance, 2)));
        }

        return points;
    }

    private async Task<(decimal AvgIncome, decimal AvgExpense)> GetHistoricalMonthlyAveragesAsync(
        Guid userId,
        DateOnly currentMonthStart,
        CancellationToken ct)
    {
        var lookBackStart = currentMonthStart.AddMonths(-6);

        var history = await dbContext.Transactions
            .Where(x =>
                x.UserId == userId &&
                x.TransactionDate >= lookBackStart &&
                x.TransactionDate < currentMonthStart &&
                x.Type != TransactionType.Transfer)
            .ToListAsync(ct);

        if (history.Count == 0)
        {
            var fallback = await dbContext.Transactions
                .Where(x =>
                    x.UserId == userId &&
                    x.TransactionDate >= currentMonthStart &&
                    x.TransactionDate <= DateOnly.FromDateTime(DateTime.UtcNow.Date) &&
                    x.Type != TransactionType.Transfer)
                .ToListAsync(ct);

            return (
                fallback.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                fallback.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount));
        }

        var monthGroups = history
            .GroupBy(x => $"{x.TransactionDate.Year}-{x.TransactionDate.Month:00}")
            .ToList();

        var averageIncome = monthGroups.Average(g => g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount));
        var averageExpense = monthGroups.Average(g => g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount));

        return (averageIncome, averageExpense);
    }

    private async Task<(decimal KnownIncome, decimal KnownExpense)> GetKnownRecurringTotalsAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var recurring = await dbContext.RecurringTransactions
            .Where(x =>
                x.UserId == userId &&
                !x.IsPaused &&
                x.NextRunDate <= to &&
                (x.EndDate == null || x.EndDate >= from))
            .ToListAsync(ct);

        var knownIncome = 0m;
        var knownExpense = 0m;

        foreach (var item in recurring)
        {
            var runDate = item.NextRunDate < from ? from : item.NextRunDate;
            while (runDate <= to)
            {
                if (item.Type == TransactionType.Income)
                {
                    knownIncome += item.Amount;
                }
                else if (item.Type == TransactionType.Expense)
                {
                    knownExpense += item.Amount;
                }

                runDate = GetNextRunDate(runDate, item.Frequency);
            }
        }

        return (knownIncome, knownExpense);
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

    private static IReadOnlyList<string> BuildWarnings(decimal forecastedEndBalance, decimal projectedExpense, decimal currentBalance)
    {
        var warnings = new List<string>();
        if (forecastedEndBalance < 0)
        {
            warnings.Add("Negative balance likely by end of month.");
        }

        if (projectedExpense > currentBalance * 1.2m)
        {
            warnings.Add("Projected expenses are significantly higher than current balance.");
        }

        return warnings;
    }
}

