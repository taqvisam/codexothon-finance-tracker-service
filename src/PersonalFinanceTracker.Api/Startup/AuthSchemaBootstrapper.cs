using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Api.Startup;

public static class AuthSchemaBootstrapper
{
    public static async Task EnsureCriticalColumnsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AuthSchemaBootstrapper");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var commands = new[]
        {
            """ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "IsSoftDeleted" boolean NOT NULL DEFAULT FALSE;""",
            """ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "SoftDeletedAt" timestamp with time zone NULL;""",
            """ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "ShowWelcomeBackMessage" boolean NOT NULL DEFAULT FALSE;""",
            """ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "OnboardingWorkbookEmailSentAt" timestamp with time zone NULL;"""
        };

        foreach (var command in commands)
        {
            await dbContext.Database.ExecuteSqlRawAsync(command, cancellationToken);
        }

        logger.LogInformation("Critical auth schema backfill completed.");
    }
}
