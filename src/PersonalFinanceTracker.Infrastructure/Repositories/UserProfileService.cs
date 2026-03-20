using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Auth;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class UserProfileService(AppDbContext dbContext) : IUserProfileService
{
    public async Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        return new UserProfileResponse(
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.ProfileImageUrl
        );
    }

    public async Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken ct = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new AppException("User not found.", 404);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new AppException("Email is required.");
        }

        var duplicateEmailExists = await dbContext.Users.AnyAsync(
            x => x.Id != userId && x.Email == normalizedEmail,
            ct
        );
        if (duplicateEmailExists)
        {
            throw new AppException("Email already exists.", 409);
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.DisplayName : request.DisplayName.Trim();
        if (displayName.Length < 2)
        {
            throw new AppException("Display name must be at least 2 characters.");
        }

        var profileImage = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();
        if (profileImage is not null && profileImage.Length > 3_000_000)
        {
            throw new AppException("Profile image is too large.");
        }

        user.DisplayName = displayName;
        user.Email = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.ProfileImageUrl = profileImage;

        await dbContext.SaveChangesAsync(ct);

        return new UserProfileResponse(
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.ProfileImageUrl
        );
    }
}

