using PersonalFinanceTracker.Application.DTOs.Auth;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileResponse> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserProfileResponse> UpdateAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, DeleteUserAccountRequest request, CancellationToken ct = default);
}
