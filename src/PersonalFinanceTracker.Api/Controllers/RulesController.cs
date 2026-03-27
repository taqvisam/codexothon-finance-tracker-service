using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Rules;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rules")]
public class RulesController(IRuleService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await ruleService.GetAllAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RuleRequest request, CancellationToken ct)
        => Ok(await ruleService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] RuleRequest request, CancellationToken ct)
        => Ok(await ruleService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await ruleService.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
