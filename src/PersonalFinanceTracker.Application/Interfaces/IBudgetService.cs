using PersonalFinanceTracker.Application.DTOs.Budgets;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetResponse>> GetAllAsync(Guid userId, int? month, int? year, CancellationToken ct = default);
    Task<BudgetResponse> CreateAsync(Guid userId, BudgetRequest request, CancellationToken ct = default);
    Task<BudgetResponse> UpdateAsync(Guid userId, Guid id, BudgetRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task<int> DuplicateLastMonthAsync(Guid userId, int month, int year, CancellationToken ct = default);
}
