using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class GoalContributionTests
{
    [Fact]
    public async Task ContributeAsync_Should_Require_Account_When_Goal_Has_No_Linked_Account()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var goalId = await SeedGoalAsync(dbContext, userId, linkedAccountId: null, currentAmount: 100m, targetAmount: 1000m);
        var service = CreateGoalService(dbContext);

        var act = () => service.ContributeAsync(userId, goalId, 50m, null);

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.Message.Should().Be("Select an account for this goal contribution.");
        exception.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ContributeAsync_Should_Create_Expense_Transaction_And_Deduct_Selected_Account()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var accountId = await SeedAccountAsync(dbContext, userId, "Salary Account", 5000m);
        var goalId = await SeedGoalAsync(dbContext, userId, linkedAccountId: null, currentAmount: 100m, targetAmount: 1000m);
        var service = CreateGoalService(dbContext);

        var response = await service.ContributeAsync(userId, goalId, 250m, accountId);

        response.CurrentAmount.Should().Be(350m);

        var account = await dbContext.Accounts.SingleAsync(x => x.Id == accountId);
        account.CurrentBalance.Should().Be(4750m);

        var transaction = await dbContext.Transactions.SingleAsync();
        transaction.AccountId.Should().Be(accountId);
        transaction.Type.Should().Be(TransactionType.Expense);
        transaction.Amount.Should().Be(250m);
        transaction.Merchant.Should().Be("Emergency Fund");
        transaction.Note.Should().Be("Goal contribution: Emergency Fund");

        var category = await dbContext.Categories.SingleAsync();
        category.Name.Should().Be("Goal Contribution");
        category.Type.Should().Be(CategoryType.Expense);
    }

    [Fact]
    public async Task WithdrawAsync_Should_Create_Income_Transaction_And_Credit_Selected_Account()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var accountId = await SeedAccountAsync(dbContext, userId, "Savings", 2000m);
        var goalId = await SeedGoalAsync(dbContext, userId, linkedAccountId: null, currentAmount: 600m, targetAmount: 1000m);
        var service = CreateGoalService(dbContext);

        var response = await service.WithdrawAsync(userId, goalId, 150m, accountId);

        response.CurrentAmount.Should().Be(450m);

        var account = await dbContext.Accounts.SingleAsync(x => x.Id == accountId);
        account.CurrentBalance.Should().Be(2150m);

        var transaction = await dbContext.Transactions.SingleAsync();
        transaction.AccountId.Should().Be(accountId);
        transaction.Type.Should().Be(TransactionType.Income);
        transaction.Amount.Should().Be(150m);
        transaction.Note.Should().Be("Goal withdrawal: Emergency Fund");

        var category = await dbContext.Categories.SingleAsync();
        category.Name.Should().Be("Goal Withdrawal");
        category.Type.Should().Be(CategoryType.Income);
    }

    private static GoalService CreateGoalService(AppDbContext dbContext)
    {
        return new GoalService(
            dbContext,
            new AccessControlService(dbContext),
            new AccountActivityLogger(dbContext));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"goal-tests-{Guid.NewGuid():N}@example.com",
            DisplayName = "Goal Test User",
            PasswordHash = "hash"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> SeedAccountAsync(AppDbContext dbContext, Guid userId, string name, decimal openingBalance)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = AccountType.Bank,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> SeedGoalAsync(
        AppDbContext dbContext,
        Guid userId,
        Guid? linkedAccountId,
        decimal currentAmount,
        decimal targetAmount)
    {
        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Emergency Fund",
            CurrentAmount = currentAmount,
            TargetAmount = targetAmount,
            LinkedAccountId = linkedAccountId,
            Status = "active"
        };

        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync();
        return goal.Id;
    }
}
