using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var auth = await authService.RegisterAsync(request, ct);
        WriteRefreshCookie(auth.RefreshToken);
        return Ok(auth);
    }

    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var auth = await authService.LoginAsync(request, ct);
        WriteRefreshCookie(auth.RefreshToken);
        return Ok(auth);
    }

    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request, CancellationToken ct)
    {
        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Request.Cookies["refreshToken"];
        }

        var auth = await authService.RefreshAsync(new RefreshRequest(token ?? string.Empty), ct);
        WriteRefreshCookie(auth.RefreshToken);
        return Ok(auth);
    }

    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("oauth")]
    public async Task<IActionResult> OAuth([FromBody] OAuthLoginRequest request, CancellationToken ct)
    {
        var auth = await authService.OAuthLoginAsync(request, ct);
        WriteRefreshCookie(auth.RefreshToken);
        return Ok(auth);
    }

    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Ok(new { message = "If account exists, reset instructions have been sent." });
    }

    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return Ok(new { message = "Password reset successful." });
    }

    [Authorize]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await authService.ChangePasswordAsync(User.GetUserId(), request, ct);
        return Ok(new { message = "Password changed successfully. Please login again." });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(User.GetUserId(), ct);
        Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/auth" });
        return Ok(new { message = "Logged out." });
    }

    private void WriteRefreshCookie(string refreshToken)
    {
        var isDev = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDev,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(14),
            Path = "/api/auth"
        });
    }
}
