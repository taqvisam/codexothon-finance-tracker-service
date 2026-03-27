using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonalFinanceTracker.Api.HealthChecks;

public sealed class GoogleOAuthHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var clientId = configuration["OAuth:Google:ClientId"];
        var isConfigured = !string.IsNullOrWhiteSpace(clientId);

        var result = isConfigured
            ? HealthCheckResult.Healthy("Google OAuth is configured.")
            : HealthCheckResult.Degraded("Google OAuth client id is missing.");

        return Task.FromResult(result);
    }
}
