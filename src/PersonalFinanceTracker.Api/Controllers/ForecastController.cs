using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/forecast")]
public class ForecastController(IForecastService forecastService) : ControllerBase
{
    [HttpGet("month")]
    public async Task<IActionResult> GetMonth(CancellationToken ct)
        => Ok(await forecastService.GetMonthForecastAsync(User.GetUserId(), ct));

    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily(CancellationToken ct)
        => Ok(await forecastService.GetDailyForecastAsync(User.GetUserId(), ct));
}

