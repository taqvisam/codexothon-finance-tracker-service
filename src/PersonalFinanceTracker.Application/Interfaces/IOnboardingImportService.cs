using PersonalFinanceTracker.Application.DTOs.Onboarding;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IOnboardingImportService
{
    Task<OnboardingImportResponse> ImportAsync(Guid userId, OnboardingImportRequest request, CancellationToken ct = default);
}
