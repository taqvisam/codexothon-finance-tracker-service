using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.BackgroundJobs;

public class RecurringTransactionWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringTransactionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                var items = await db.RecurringTransactions
                    .Where(x => !x.IsPaused && x.AutoCreateTransaction && x.NextRunDate <= today && (!x.EndDate.HasValue || x.EndDate >= today))
                    .ToListAsync(stoppingToken);

                foreach (var item in items)
                {
                    if (!item.AccountId.HasValue) continue;
                    var account = await db.Accounts.FirstOrDefaultAsync(x => x.Id == item.AccountId && x.UserId == item.UserId, stoppingToken);
                    if (account is null) continue;

                    db.Transactions.Add(new Transaction
                    {
                        UserId = item.UserId,
                        AccountId = item.AccountId.Value,
                        CategoryId = item.CategoryId,
                        Type = item.Type,
                        Amount = item.Amount,
                        TransactionDate = today,
                        Merchant = item.Title,
                        Note = "Auto-generated from recurring transaction",
                        RecurringTransactionId = item.Id
                    });

                    account.CurrentBalance += item.Type == TransactionType.Income ? item.Amount : -item.Amount;
                    account.LastUpdatedAt = DateTime.UtcNow;
                    item.NextRunDate = GetNextDate(item.NextRunDate, item.Frequency);
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Recurring worker failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private static DateOnly GetNextDate(DateOnly value, RecurringFrequency frequency)
    {
        return frequency switch
        {
            RecurringFrequency.Daily => value.AddDays(1),
            RecurringFrequency.Weekly => value.AddDays(7),
            RecurringFrequency.Monthly => value.AddMonths(1),
            RecurringFrequency.Yearly => value.AddYears(1),
            _ => value.AddMonths(1)
        };
    }
}
