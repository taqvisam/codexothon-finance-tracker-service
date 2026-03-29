using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Api.HealthChecks;

public sealed class AuthSchemaHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Probe auth-critical user columns so schema drift shows up in /health.
            await dbContext.Users
                .AsNoTracking()
                .Select(user => new
                {
                    user.Id,
                    user.Email,
                    user.IsSoftDeleted,
                    user.ShowWelcomeBackMessage,
                    user.OnboardingWorkbookEmailSentAt
                })
                .Take(1)
                .ToListAsync(cancellationToken);

            return HealthCheckResult.Healthy("Auth user schema is queryable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Auth user schema check failed.", ex);
        }
    }
}
