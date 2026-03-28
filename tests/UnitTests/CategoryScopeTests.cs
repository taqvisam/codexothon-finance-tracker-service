using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class CategoryScopeTests
{
    [Fact]
    public async Task GetAllAsync_WithAccountScope_Should_Return_Only_SharedAccountOwnerCategories()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedUserAsync(dbContext, "owner");
        var collaboratorId = await SeedUserAsync(dbContext, "collab");
        var accountId = await SeedAccountAsync(dbContext, ownerId, "Family Checking");

        dbContext.AccountMembers.Add(new AccountMember
        {
            AccountId = accountId,
            UserId = collaboratorId,
            Role = AccountMemberRole.Editor
        });

        await SeedCategoryAsync(dbContext, ownerId, "Groceries", CategoryType.Expense);
        await SeedCategoryAsync(dbContext, collaboratorId, "Groceries", CategoryType.Expense);
        await SeedCategoryAsync(dbContext, collaboratorId, "Salary", CategoryType.Income);
        await dbContext.SaveChangesAsync();

        var service = new CategoryService(dbContext, new AccessControlService(dbContext));

        var result = await service.GetAllAsync(collaboratorId, accountId: accountId);

        result.Select(category => category.Name).Should().BeEquivalentTo(["Groceries"]);
        result.All(category => category.Type == CategoryType.Expense).Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WithEditableOnly_Should_Return_Only_CurrentUsersCategories()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedUserAsync(dbContext, "owner");
        var collaboratorId = await SeedUserAsync(dbContext, "collab");

        await SeedCategoryAsync(dbContext, ownerId, "Groceries", CategoryType.Expense);
        await SeedCategoryAsync(dbContext, collaboratorId, "Dining", CategoryType.Expense);
        await SeedCategoryAsync(dbContext, collaboratorId, "Salary", CategoryType.Income);
        await dbContext.SaveChangesAsync();

        var service = new CategoryService(dbContext, new AccessControlService(dbContext));

        var result = await service.GetAllAsync(collaboratorId, editableOnly: true);

        result.Select(category => category.Name).Should().BeEquivalentTo(["Dining", "Salary"]);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext dbContext, string prefix)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{prefix}-{Guid.NewGuid():N}@example.com",
            DisplayName = prefix,
            PasswordHash = "hash"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> SeedAccountAsync(AppDbContext dbContext, Guid userId, string name)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = AccountType.Bank,
            OpeningBalance = 1000m,
            CurrentBalance = 1000m
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    private static async Task SeedCategoryAsync(AppDbContext dbContext, Guid userId, string name, CategoryType type)
    {
        dbContext.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = type,
            Color = "#2f6fbe",
            Icon = "wallet",
            IsArchived = false
        });

        await dbContext.SaveChangesAsync();
    }
}
