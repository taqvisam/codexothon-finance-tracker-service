using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController(IUserProfileService userProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await userProfileService.GetAsync(User.GetUserId(), ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateUserProfileRequest request, CancellationToken ct)
        => Ok(await userProfileService.UpdateAsync(User.GetUserId(), request, ct));

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] DeleteUserAccountRequest request, CancellationToken ct)
    {
        await userProfileService.DeleteAsync(User.GetUserId(), request, ct);
        return Ok(new
        {
            message = request.DeleteData
                ? "Your account and data were deleted permanently."
                : "Your account was removed, but your data is preserved if you sign in again."
        });
    }
}
