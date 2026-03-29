using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class AccountDeleteTests
{
    [Fact]
    public async Task DeleteAsync_Should_Require_Force_When_Account_Has_Linked_Data()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = $"delete-force-{Guid.NewGuid():N}@example.com",
            DisplayName = "Delete Force User",
            PasswordHash = "hash"
        });
        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = userId,
            Name = "Primary",
            Type = AccountType.Bank,
            OpeningBalance = 1000m,
            CurrentBalance = 900m
        });
        dbContext.Transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = accountId,
            Type = TransactionType.Expense,
            Amount = 100m,
            TransactionDate = new DateOnly(2026, 3, 1)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.DeleteAsync(userId, accountId, false);

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.StatusCode.Should().Be(409);
        exception.Message.Should().Contain("Confirm forced deletion");
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Linked_Data_And_Recalculate_Remaining_Balances()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var sourceAccountId = Guid.NewGuid();
        var targetAccountId = Guid.NewGuid();
        var transferId = Guid.NewGuid();
        var targetExpenseId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = $"delete-cascade-{Guid.NewGuid():N}@example.com",
            DisplayName = "Delete Cascade User",
            PasswordHash = "hash"
        });
        dbContext.Accounts.AddRange(
            new Account
            {
                Id = sourceAccountId,
                UserId = userId,
                Name = "Primary",
                Type = AccountType.Bank,
                OpeningBalance = 1000m,
                CurrentBalance = 800m
            },
            new Account
            {
                Id = targetAccountId,
                UserId = userId,
                Name = "Savings",
                Type = AccountType.Savings,
                OpeningBalance = 500m,
                CurrentBalance = 650m
            });
        dbContext.Transactions.AddRange(
            new Transaction
            {
                Id = transferId,
                UserId = userId,
                AccountId = sourceAccountId,
                TransferAccountId = targetAccountId,
                Type = TransactionType.Transfer,
                Amount = 200m,
                TransactionDate = new DateOnly(2026, 3, 2)
            },
            new Transaction
            {
                Id = targetExpenseId,
                UserId = userId,
                AccountId = targetAccountId,
                Type = TransactionType.Expense,
                Amount = 50m,
                TransactionDate = new DateOnly(2026, 3, 3)
            });
        dbContext.Goals.Add(new Goal
        {
            Id = goalId,
            UserId = userId,
            Name = "Emergency Fund",
            TargetAmount = 5000m,
            CurrentAmount = 300m,
            LinkedAccountId = sourceAccountId
        });
        dbContext.Budgets.Add(new Budget
        {
            Id = budgetId,
            UserId = userId,
            AccountId = sourceAccountId,
            CategoryId = Guid.NewGuid(),
            Month = 3,
            Year = 2026,
            Amount = 1000m
        });
        dbContext.RecurringTransactions.Add(new RecurringTransaction
        {
            UserId = userId,
            Title = "Gym",
            Type = TransactionType.Expense,
            Amount = 100m,
            AccountId = sourceAccountId,
            Frequency = RecurringFrequency.Monthly,
            StartDate = new DateOnly(2026, 3, 1),
            NextRunDate = new DateOnly(2026, 4, 1)
        });
        dbContext.AccountActivities.AddRange(
            new AccountActivity
            {
                AccountId = sourceAccountId,
                ActorUserId = userId,
                EntityType = "transaction",
                EntityId = transferId,
                Action = "transfer",
                Description = "Transferred 200."
            },
            new AccountActivity
            {
                AccountId = targetAccountId,
                ActorUserId = userId,
                EntityType = "transaction",
                EntityId = transferId,
                Action = "transfer",
                Description = "Received 200."
            },
            new AccountActivity
            {
                AccountId = sourceAccountId,
                ActorUserId = userId,
                EntityType = "goal",
                EntityId = goalId,
                Action = "created",
                Description = "Created goal."
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.DeleteAsync(userId, sourceAccountId, true);

        (await dbContext.Accounts.AnyAsync(x => x.Id == sourceAccountId)).Should().BeFalse();
        (await dbContext.Transactions.AnyAsync(x => x.Id == transferId)).Should().BeFalse();
        (await dbContext.Goals.AnyAsync(x => x.Id == goalId)).Should().BeFalse();
        (await dbContext.Budgets.AnyAsync(x => x.Id == budgetId)).Should().BeFalse();
        (await dbContext.RecurringTransactions.AnyAsync(x => x.AccountId == sourceAccountId)).Should().BeFalse();
        (await dbContext.AccountActivities.AnyAsync(x => x.EntityId == transferId || x.EntityId == goalId)).Should().BeFalse();

        var remainingAccount = await dbContext.Accounts.SingleAsync(x => x.Id == targetAccountId);
        remainingAccount.CurrentBalance.Should().Be(450m);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static AccountService CreateService(AppDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = "https://example.test"
            })
            .Build();
        return new AccountService(dbContext, new AccessControlService(dbContext), new AccountActivityLogger(dbContext), new NoOpEmailSender(), configuration);
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(AppEmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }
}
