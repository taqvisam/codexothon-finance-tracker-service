using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonalFinanceTracker.Api.HealthChecks;

public sealed class EmailHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var provider = configuration["Email:Provider"];
        if (string.IsNullOrWhiteSpace(provider))
        {
            return Task.FromResult(HealthCheckResult.Degraded("Email provider is not configured."));
        }

        if (string.Equals(provider, "AzureCommunicationServices", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration["Email:AzureCommunicationServices:ConnectionString"];
            var fromAddress = configuration["Email:FromAddress"];
            var appBaseUrl = configuration["App:BaseUrl"];

            var configured =
                !string.IsNullOrWhiteSpace(connectionString) &&
                !string.IsNullOrWhiteSpace(fromAddress) &&
                !string.IsNullOrWhiteSpace(appBaseUrl);

            return Task.FromResult(
                configured
                    ? HealthCheckResult.Healthy("Azure Communication Services email is configured.")
                    : HealthCheckResult.Degraded("Azure Communication Services email settings are incomplete."));
        }

        return Task.FromResult(HealthCheckResult.Degraded($"Unsupported email provider '{provider}'."));
    }
}
