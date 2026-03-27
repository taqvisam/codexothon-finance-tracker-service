namespace PersonalFinanceTracker.Application.DTOs.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public record OAuthLoginRequest(
    string Provider,
    string? IdToken,
    string? ExternalUserId,
    string? Email,
    string? DisplayName);
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string Email,
    string DisplayName,
    string? ProfileImageUrl);

public record UserProfileResponse(
    string Email,
    string DisplayName,
    string? PhoneNumber,
    string? ProfileImageUrl);

public record UpdateUserProfileRequest(
    string DisplayName,
    string Email,
    string? PhoneNumber,
    string? ProfileImageUrl);
