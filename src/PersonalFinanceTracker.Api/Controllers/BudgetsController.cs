using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Budgets;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budgets")]
public class BudgetsController(IBudgetService budgetService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? month, [FromQuery] int? year, CancellationToken ct)
        => Ok(await budgetService.GetAllAsync(User.GetUserId(), month, year, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BudgetRequest request, CancellationToken ct)
        => Ok(await budgetService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] BudgetRequest request, CancellationToken ct)
        => Ok(await budgetService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await budgetService.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPost("duplicate-last-month")]
    public async Task<IActionResult> DuplicateLastMonth([FromQuery] int month, [FromQuery] int year, CancellationToken ct)
    {
        var count = await budgetService.DuplicateLastMonthAsync(User.GetUserId(), month, year, ct);
        return Ok(new { created = count });
    }
}
