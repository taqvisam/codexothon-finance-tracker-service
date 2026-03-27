using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Infrastructure.BackgroundJobs;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace PersonalFinanceTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var postgresConnectionString = ResolvePostgresConnectionString(config);

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(postgresConnectionString));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IRecurringService, RecurringService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IInsightsService, InsightsService>();
        services.AddScoped<IAccessControlService, AccessControlService>();
        services.AddScoped<IRuleService, RuleService>();
        services.AddScoped<IRuleEngineService, RuleEngineService>();
        services.AddScoped<AccountActivityLogger>();

        services.AddHostedService<RecurringTransactionWorker>();

        return services;
    }

    private static string ResolvePostgresConnectionString(IConfiguration config)
    {
        var jdbcUrl =
            config["Database:JdbcUrl"] ??
            config["Database:Url"] ??
            config["JDBC_DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(jdbcUrl))
        {
            var explicitConnectionString = config.GetConnectionString("Postgres");
            if (!string.IsNullOrWhiteSpace(explicitConnectionString))
            {
                return explicitConnectionString;
            }

            throw new InvalidOperationException(
                "Missing database configuration. Set Database:JdbcUrl " +
                "(optionally with Database:Username/Database:Password) or ConnectionStrings:Postgres.");
        }

        var normalizedUrl = jdbcUrl.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase)
            ? jdbcUrl["jdbc:".Length..]
            : jdbcUrl;

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Database:JdbcUrl is not a valid JDBC/URI value.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/"
                ? "postgres"
                : uri.AbsolutePath.TrimStart('/')
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfoParts = uri.UserInfo.Split(':', 2);
            if (userInfoParts.Length > 0 && !string.IsNullOrWhiteSpace(userInfoParts[0]))
            {
                builder.Username = Uri.UnescapeDataString(userInfoParts[0]);
            }

            if (userInfoParts.Length > 1 && !string.IsNullOrWhiteSpace(userInfoParts[1]))
            {
                builder.Password = Uri.UnescapeDataString(userInfoParts[1]);
            }
        }

        var username = config["Database:Username"] ?? config["Database:User"] ?? config["DB_USERNAME"];
        var password = config["Database:Password"] ?? config["DB_PASSWORD"];

        if (!string.IsNullOrWhiteSpace(username))
        {
            builder.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            builder.Password = password;
        }

        if (string.IsNullOrWhiteSpace(builder.Username) || string.IsNullOrWhiteSpace(builder.Password))
        {
            throw new InvalidOperationException(
                "Database credentials are missing. Provide username/password in Database:JdbcUrl " +
                "or set Database:Username and Database:Password.");
        }

        if (string.IsNullOrWhiteSpace(config["Database:SslMode"]) && !builder.ContainsKey("SSL Mode"))
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }
}
