using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Categories;

public record CategoryRequest(string Name, CategoryType Type, string? Color, string? Icon, bool IsArchived);
public record CategoryResponse(Guid Id, string Name, CategoryType Type, string? Color, string? Icon, bool IsArchived);
