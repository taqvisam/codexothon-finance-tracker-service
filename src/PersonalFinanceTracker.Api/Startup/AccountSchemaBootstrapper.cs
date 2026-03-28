using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Api.Startup;

public static class AccountSchemaBootstrapper
{
    public static async Task EnsureCreditLimitColumnAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AccountSchemaBootstrapper");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var commands = new[]
        {
            """ALTER TABLE "accounts" ADD COLUMN IF NOT EXISTS "CreditLimit" numeric(12,2) NULL;""",
            """UPDATE "accounts" SET "CreditLimit" = GREATEST(ABS("OpeningBalance"), ABS("CurrentBalance"), 50000) WHERE "Type" = 2 AND ("CreditLimit" IS NULL OR "CreditLimit" <= 0);"""
        };

        foreach (var command in commands)
        {
            await dbContext.Database.ExecuteSqlRawAsync(command, cancellationToken);
        }

        logger.LogInformation("Account credit-limit schema backfill completed.");
    }
}
