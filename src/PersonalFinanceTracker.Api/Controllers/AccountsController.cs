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
    public async Task<IActionResult> Get([FromQuery] DateOnly? from, CancellationToken ct)
        => Ok(await accountService.GetAllAsync(User.GetUserId(), from, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccountRequest request, CancellationToken ct)
        => Ok(await accountService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AccountRequest request, CancellationToken ct)
        => Ok(await accountService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpGet("{id:guid}/delete-impact")]
    public async Task<IActionResult> DeleteImpact(Guid id, CancellationToken ct)
        => Ok(await accountService.GetDeleteImpactAsync(User.GetUserId(), id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteAccountRequest? request, CancellationToken ct)
    {
        var deleteRelatedData = request?.DeleteRelatedData ?? false;
        await accountService.DeleteAsync(User.GetUserId(), id, deleteRelatedData, ct);
        return Ok(new
        {
            message = deleteRelatedData
                ? "Account and linked data deleted."
                : "Account deleted."
        });
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken ct)
    {
        await accountService.TransferAsync(User.GetUserId(), request, ct);
        return Ok(new { message = "Transfer completed." });
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteAccountMemberRequest request, CancellationToken ct)
    {
        await accountService.InviteMemberAsync(User.GetUserId(), id, request, ct);
        return Ok(new { message = "Member invited." });
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id, CancellationToken ct)
        => Ok(await accountService.GetMembersAsync(User.GetUserId(), id, ct));

    [HttpPut("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, Guid userId, [FromBody] UpdateAccountMemberRequest request, CancellationToken ct)
    {
        await accountService.UpdateMemberAsync(User.GetUserId(), id, userId, request, ct);
        return Ok(new { message = "Member role updated." });
    }

    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> Activity(Guid id, CancellationToken ct)
        => Ok(await accountService.GetActivityAsync(User.GetUserId(), id, ct));
}
