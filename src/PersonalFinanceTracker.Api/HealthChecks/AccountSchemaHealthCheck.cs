using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Api.HealthChecks;

public sealed class AccountSchemaHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Accounts
                .AsNoTracking()
                .Select(account => new
                {
                    account.Id,
                    account.Name,
                    account.Type,
                    account.CreditLimit
                })
                .Take(1)
                .ToListAsync(cancellationToken);

            return HealthCheckResult.Healthy("Account schema is queryable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Account schema check failed.", ex);
        }
    }
}
