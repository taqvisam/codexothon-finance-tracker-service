using PersonalFinanceTracker.Application.DTOs.Rules;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IRuleService
{
    Task<IReadOnlyList<RuleResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<RuleResponse> CreateAsync(Guid userId, RuleRequest request, CancellationToken ct = default);
    Task<RuleResponse> UpdateAsync(Guid userId, Guid id, RuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
