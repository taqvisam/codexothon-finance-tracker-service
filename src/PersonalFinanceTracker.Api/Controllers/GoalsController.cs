using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Goals;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public class GoalsController(IGoalService goalService) : ControllerBase
{
    public record HoldRequest(bool OnHold);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await goalService.GetAllAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GoalRequest request, CancellationToken ct)
        => Ok(await goalService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] GoalRequest request, CancellationToken ct)
        => Ok(await goalService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpPost("{id:guid}/contribute")]
    public async Task<IActionResult> Contribute(Guid id, [FromBody] GoalActionRequest request, CancellationToken ct)
        => Ok(await goalService.ContributeAsync(User.GetUserId(), id, request.Amount, ct));

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] GoalActionRequest request, CancellationToken ct)
        => Ok(await goalService.WithdrawAsync(User.GetUserId(), id, request.Amount, ct));

    [HttpPost("{id:guid}/hold")]
    public async Task<IActionResult> Hold(Guid id, [FromBody] HoldRequest request, CancellationToken ct)
        => Ok(await goalService.SetHoldStatusAsync(User.GetUserId(), id, request.OnHold, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await goalService.DeleteAsync(User.GetUserId(), id, ct);
        return Ok(new { message = "Goal deleted." });
    }
}
