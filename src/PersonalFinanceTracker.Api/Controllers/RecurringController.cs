using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Recurring;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recurring")]
public class RecurringController(IRecurringService recurringService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await recurringService.GetAllAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RecurringRequest request, CancellationToken ct)
        => Ok(await recurringService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RecurringRequest request, CancellationToken ct)
        => Ok(await recurringService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await recurringService.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
