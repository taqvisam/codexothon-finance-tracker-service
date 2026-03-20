using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Infrastructure.Data;

public static class DefaultCategorySeeder
{
    private static readonly (string Name, CategoryType Type, string Color, string Icon)[] Defaults =
    [
        ("Food", CategoryType.Expense, "#F2994A", "utensils"),
        ("Rent", CategoryType.Expense, "#EB5757", "home"),
        ("Utilities", CategoryType.Expense, "#5B8DEF", "bolt"),
        ("Transport", CategoryType.Expense, "#2EA05F", "car"),
        ("Shopping", CategoryType.Expense, "#A855F7", "bag"),
        ("Entertainment", CategoryType.Expense, "#FF8A4C", "film"),
        ("Salary", CategoryType.Income, "#1F77E5", "wallet"),
        ("Freelance", CategoryType.Income, "#17A2B8", "briefcase")
    ];

    public static async Task<int> SeedAsync(AppDbContext dbContext, CancellationToken ct = default)
    {
        var userIds = await dbContext.Users
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (userIds.Count == 0)
        {
            return 0;
        }

        var existing = await dbContext.Categories
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.Name, x.Type })
            .ToListAsync(ct);

        var existingSet = new HashSet<(Guid UserId, string Name, CategoryType Type)>(
            existing.Select(x => (x.UserId, Normalize(x.Name), x.Type)));

        var toCreate = new List<Category>();
        foreach (var userId in userIds)
        {
            foreach (var definition in Defaults)
            {
                var key = (userId, Normalize(definition.Name), definition.Type);
                if (existingSet.Contains(key))
                {
                    continue;
                }

                toCreate.Add(new Category
                {
                    UserId = userId,
                    Name = definition.Name,
                    Type = definition.Type,
                    Color = definition.Color,
                    Icon = definition.Icon,
                    IsArchived = false
                });

                existingSet.Add(key);
            }
        }

        if (toCreate.Count == 0)
        {
            return 0;
        }

        dbContext.Categories.AddRange(toCreate);
        await dbContext.SaveChangesAsync(ct);
        return toCreate.Count;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}
