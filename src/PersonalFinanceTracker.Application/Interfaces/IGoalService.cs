using PersonalFinanceTracker.Application.DTOs.Goals;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IGoalService
{
    Task<IReadOnlyList<GoalResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<GoalResponse> CreateAsync(Guid userId, GoalRequest request, CancellationToken ct = default);
    Task<GoalResponse> UpdateAsync(Guid userId, Guid id, GoalRequest request, CancellationToken ct = default);
    Task<GoalResponse> ContributeAsync(Guid userId, Guid id, decimal amount, Guid? accountId, CancellationToken ct = default);
    Task<GoalResponse> WithdrawAsync(Guid userId, Guid id, decimal amount, Guid? accountId, CancellationToken ct = default);
    Task<GoalResponse> SetHoldStatusAsync(Guid userId, Guid id, bool onHold, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
