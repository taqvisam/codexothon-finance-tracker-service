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

            if (!configured)
            {
                return Task.FromResult(HealthCheckResult.Degraded("Azure Communication Services email settings are incomplete."));
            }

            var workbookPath = configuration["Email:OnboardingWorkbookPath"];
            if (string.IsNullOrWhiteSpace(workbookPath))
            {
                workbookPath = Path.Combine(AppContext.BaseDirectory, "Assets", "sample-onboarding-import-v2.xlsx");
            }

            if (!File.Exists(workbookPath))
            {
                return Task.FromResult(HealthCheckResult.Degraded("Azure Communication Services email is configured, but onboarding workbook attachment asset is missing."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Azure Communication Services email is configured and onboarding workbook asset is present."));
        }

        return Task.FromResult(HealthCheckResult.Degraded($"Unsupported email provider '{provider}'."));
    }
}
