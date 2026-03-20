using PersonalFinanceTracker.Application.DTOs.Recurring;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IRecurringService
{
    Task<IReadOnlyList<RecurringResponse>> GetAllAsync(Guid userId, CancellationToken ct = default);
    Task<RecurringResponse> CreateAsync(Guid userId, RecurringRequest request, CancellationToken ct = default);
    Task<RecurringResponse> UpdateAsync(Guid userId, Guid id, RecurringRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
