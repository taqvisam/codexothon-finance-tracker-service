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

    [HttpGet]
    public async Task<IActionResult> GetHighlights(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
        => Ok(await insightsService.GetHighlightsAsync(User.GetUserId(), from, to, accountId, categoryId, ct));
}
