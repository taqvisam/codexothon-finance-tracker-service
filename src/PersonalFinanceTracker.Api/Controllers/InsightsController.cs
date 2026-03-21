using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/insights")]
public class InsightsController(IInsightsService insightsService) : ControllerBase
{
    [HttpGet("health-score")]
    public async Task<IActionResult> GetHealthScore(CancellationToken ct)
        => Ok(await insightsService.GetHealthScoreAsync(User.GetUserId(), ct));
}

