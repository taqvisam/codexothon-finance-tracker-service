using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Categories;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class CategoryService(AppDbContext dbContext, IAccessControlService accessControlService) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var ownerIds = await accessControlService.GetAccessibleAccountOwnerIdsAsync(userId, ct);
        return await dbContext.Categories
            .Where(x => ownerIds.Contains(x.UserId))
            .OrderBy(x => x.Name)
            .Select(x => new CategoryResponse(x.Id, x.Name, x.Type, x.Color, x.Icon, x.IsArchived))
            .ToListAsync(ct);
    }

    public async Task<CategoryResponse> CreateAsync(Guid userId, CategoryRequest request, CancellationToken ct = default)
    {
        var category = new Category
        {
            UserId = userId,
            Name = request.Name,
            Type = request.Type,
            Color = request.Color,
            Icon = request.Icon,
            IsArchived = request.IsArchived
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(ct);
        return new CategoryResponse(category.Id, category.Name, category.Type, category.Color, category.Icon, category.IsArchived);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid userId, Guid id, CategoryRequest request, CancellationToken ct = default)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Category not found.", 404);

        category.Name = request.Name;
        category.Type = request.Type;
        category.Color = request.Color;
        category.Icon = request.Icon;
        category.IsArchived = request.IsArchived;

        await dbContext.SaveChangesAsync(ct);
        return new CategoryResponse(category.Id, category.Name, category.Type, category.Color, category.Icon, category.IsArchived);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Category not found.", 404);
        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(ct);
    }
}
