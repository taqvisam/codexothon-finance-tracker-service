using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Onboarding;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/onboarding")]
public class OnboardingController(IOnboardingImportService onboardingImportService) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] OnboardingImportRequest request, CancellationToken ct)
        => Ok(await onboardingImportService.ImportAsync(User.GetUserId(), request, ct));
}
