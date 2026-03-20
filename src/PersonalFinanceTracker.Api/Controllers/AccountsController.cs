using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Accounts;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public class AccountsController(IAccountService accountService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await accountService.GetAllAsync(User.GetUserId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccountRequest request, CancellationToken ct)
        => Ok(await accountService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AccountRequest request, CancellationToken ct)
        => Ok(await accountService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await accountService.DeleteAsync(User.GetUserId(), id, ct);
        return Ok(new { message = "Account deleted." });
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken ct)
    {
        await accountService.TransferAsync(User.GetUserId(), request, ct);
        return Ok(new { message = "Transfer completed." });
    }
}
