using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using FluentValidation;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Application.Validators;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class AuthService(AppDbContext dbContext, IConfiguration configuration) : IAuthService
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly RefreshRequestValidator _refreshValidator = new();
    private readonly ForgotPasswordRequestValidator _forgotPasswordValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();
    private readonly OAuthLoginRequestValidator _oauthValidator = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await _registerValidator.ValidateAndThrowAsync(request, ct);

        if (await dbContext.Users.AnyAsync(x => x.Email == request.Email, ct))
        {
            throw new AppException("Email already exists.", 409);
        }

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        dbContext.Users.Add(user);
        AddDefaultCategories(user.Id);
        await dbContext.SaveChangesAsync(ct);

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new AppException("Invalid email/password.", 401);

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new AppException("Invalid email/password.", 401);
        }

        var passwordValid = false;
        try
        {
            passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }
        catch
        {
            passwordValid = false;
        }

        if (!passwordValid)
        {
            throw new AppException("Invalid email/password.", 401);
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        await _refreshValidator.ValidateAndThrowAsync(request, ct);

        var (refreshUserId, _) = ParseRefreshToken(request.RefreshToken);
        User? user = null;
        if (refreshUserId.HasValue)
        {
            user = await dbContext.Users.FirstOrDefaultAsync(x =>
                x.Id == refreshUserId.Value &&
                x.RefreshTokenHash != null &&
                x.RefreshTokenExpiresAt > DateTime.UtcNow, ct);
        }

        if (user is null)
        {
            // Backward compatibility for previously issued refresh tokens without user prefix.
            var users = await dbContext.Users
                .Where(x => x.RefreshTokenHash != null && x.RefreshTokenExpiresAt > DateTime.UtcNow)
                .ToListAsync(ct);
            user = users.FirstOrDefault(x => BCrypt.Net.BCrypt.Verify(request.RefreshToken, x.RefreshTokenHash));
        }

        if (user is null || user.RefreshTokenHash is null || !BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshTokenHash))
        {
            throw new AppException("Invalid refresh token.", 401);
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> OAuthLoginAsync(OAuthLoginRequest request, CancellationToken ct = default)
    {
        await _oauthValidator.ValidateAndThrowAsync(request, ct);

        if (!string.Equals(request.Provider, "google", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Unsupported OAuth provider.");
        }

        string? normalizedEmail = null;
        string? displayName = request.DisplayName;
        string? profileImageUrl = null;

        if (!string.IsNullOrWhiteSpace(request.IdToken))
        {
            var payload = await VerifyGoogleTokenAsync(request.IdToken, ct);
            normalizedEmail = payload.Email?.Trim().ToLowerInvariant();
            displayName = string.IsNullOrWhiteSpace(displayName) ? payload.Name : displayName;
            profileImageUrl = payload.Picture;
        }
        else
        {
            var allowInsecureTestMode = bool.TryParse(configuration["OAuth:AllowInsecureTestMode"], out var value) && value;
            if (!allowInsecureTestMode)
            {
                throw new AppException("OAuth id token is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new AppException("OAuth email is required in test mode.");
            }

            normalizedEmail = request.Email.Trim().ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new AppException("OAuth email is required.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, ct);
        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "OAuth User" : displayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
                ProfileImageUrl = string.IsNullOrWhiteSpace(profileImageUrl) ? null : profileImageUrl
            };
            dbContext.Users.Add(user);
            AddDefaultCategories(user.Id);
            await dbContext.SaveChangesAsync(ct);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                user.DisplayName = displayName;
            }

            if (!string.IsNullOrWhiteSpace(profileImageUrl))
            {
                // Keep Google profile image in sync for OAuth sign-ins.
                user.ProfileImageUrl = profileImageUrl;
            }
        }

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        await _forgotPasswordValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null)
        {
            return;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        user.ResetPasswordTokenHash = BCrypt.Net.BCrypt.HashPassword(token);
        user.ResetPasswordTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
        await dbContext.SaveChangesAsync(ct);

        if (bool.TryParse(configuration["Auth:LogResetTokenForDemo"], out var logToken) && logToken)
        {
            Console.WriteLine($"[DEMO] Reset token for {email}: {token}");
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        await _resetPasswordValidator.ValidateAndThrowAsync(request, ct);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, ct)
            ?? throw new AppException("User not found.", 404);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new AppException("Reset token is required.");
        }

        if (user.ResetPasswordTokenHash is null || user.ResetPasswordTokenExpiresAt is null || user.ResetPasswordTokenExpiresAt < DateTime.UtcNow)
        {
            throw new AppException("Reset token is invalid or expired.", 401);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Token, user.ResetPasswordTokenHash))
        {
            throw new AppException("Reset token is invalid or expired.", 401);
        }

        if (request.NewPassword.Length < 8
            || !request.NewPassword.Any(char.IsUpper)
            || !request.NewPassword.Any(char.IsLower)
            || !request.NewPassword.Any(char.IsDigit))
        {
            throw new AppException("Password must be at least 8 chars with upper, lower, number.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ResetPasswordTokenHash = null;
        user.ResetPasswordTokenExpiresAt = null;
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        await _changePasswordValidator.ValidateAndThrowAsync(request, ct);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            throw new AppException("Password change is unavailable for this account.", 400);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new AppException("Current password is incorrect.", 401);
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            throw new AppException("New password must be different from current password.", 400);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAt = null;
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, CancellationToken ct)
    {
        var displayName = NormalizeDisplayName(user);
        var expires = DateTime.UtcNow.AddHours(1);
        var accessToken = CreateJwt(user, displayName, expires);
        var refreshSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshToken = $"{user.Id:N}.{refreshSecret}";

        user.DisplayName = displayName;
        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(14);
        await dbContext.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, expires, user.Email, displayName, user.ProfileImageUrl);
    }

    private string CreateJwt(User user, string displayName, DateTime expires)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "PersonalFinanceTracker";
        var audience = configuration["Jwt:Audience"] ?? "PersonalFinanceTrackerClients";
        var key = configuration["Jwt:Key"] ?? "ChangeThisInProduction_AtLeast32Chars";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var token = new JwtSecurityToken(
            issuer,
            audience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("displayName", displayName)
            },
            expires: expires,
            signingCredentials: new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(string idToken, CancellationToken ct)
    {
        var googleClientId = configuration["OAuth:Google:ClientId"];
        var settings = new GoogleJsonWebSignature.ValidationSettings();
        if (!string.IsNullOrWhiteSpace(googleClientId))
        {
            settings.Audience = new[] { googleClientId };
        }

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch
        {
            ct.ThrowIfCancellationRequested();
            throw new AppException("Invalid OAuth token.", 401);
        }
    }

    private void AddDefaultCategories(Guid userId)
    {
        var expenseDefaults = new[]
        {
            "Food", "Rent", "Utilities", "Transport", "Entertainment", "Shopping",
            "Health", "Education", "Travel", "Subscriptions", "Miscellaneous"
        };
        var incomeDefaults = new[] { "Salary", "Freelance", "Bonus", "Investment", "Gift", "Refund", "Other" };

        dbContext.Categories.AddRange(expenseDefaults.Select(name => new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = CategoryType.Expense,
            IsArchived = false
        }));
        dbContext.Categories.AddRange(incomeDefaults.Select(name => new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = CategoryType.Income,
            IsArchived = false
        }));
    }

    private static (Guid? userId, string? tokenSecret) ParseRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, null);
        }

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return (null, null);
        }

        if (!Guid.TryParseExact(parts[0], "N", out var userId))
        {
            return (null, null);
        }

        return (userId, parts[1]);
    }

    private static string NormalizeDisplayName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        var at = user.Email.IndexOf('@');
        var localPart = at > 0 ? user.Email[..at] : user.Email;
        if (!string.IsNullOrWhiteSpace(localPart))
        {
            return localPart.Trim();
        }

        return "User";
    }
}
