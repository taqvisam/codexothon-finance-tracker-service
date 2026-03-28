using PersonalFinanceTracker.Application.DTOs.Categories;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(Guid userId, Guid? accountId = null, bool editableOnly = false, CancellationToken ct = default);
    Task<CategoryResponse> CreateAsync(Guid userId, CategoryRequest request, CancellationToken ct = default);
    Task<CategoryResponse> UpdateAsync(Guid userId, Guid id, CategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
}
