using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class AccountPeriodStartBalanceTests
{
    [Fact]
    public async Task GetAllAsync_Should_Return_Balance_At_Selected_Period_Start()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var bankAccountId = Guid.NewGuid();
        var transferTargetId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "period-balance@example.com",
            DisplayName = "Period Balance User",
            PasswordHash = "hash"
        });
        dbContext.Accounts.AddRange(
            new Account
            {
                Id = bankAccountId,
                UserId = userId,
                Name = "Main Bank",
                Type = AccountType.Bank,
                OpeningBalance = 1000m,
                CurrentBalance = 1430m
            },
            new Account
            {
                Id = transferTargetId,
                UserId = userId,
                Name = "Savings",
                Type = AccountType.Savings,
                OpeningBalance = 500m,
                CurrentBalance = 420m
            });
        dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = userId,
                AccountId = bankAccountId,
                Type = TransactionType.Income,
                Amount = 400m,
                TransactionDate = new DateOnly(2026, 2, 2)
            },
            new Transaction
            {
                UserId = userId,
                AccountId = bankAccountId,
                Type = TransactionType.Expense,
                Amount = 150m,
                TransactionDate = new DateOnly(2026, 2, 9)
            },
            new Transaction
            {
                UserId = userId,
                AccountId = bankAccountId,
                TransferAccountId = transferTargetId,
                Type = TransactionType.Transfer,
                Amount = 100m,
                TransactionDate = new DateOnly(2026, 2, 16)
            },
            new Transaction
            {
                UserId = userId,
                AccountId = bankAccountId,
                Type = TransactionType.Expense,
                Amount = 75m,
                TransactionDate = new DateOnly(2026, 3, 4)
            });
        await dbContext.SaveChangesAsync();

        var service = new AccountService(
            dbContext,
            new AccessControlService(dbContext),
            new AccountActivityLogger(dbContext),
            new NoOpEmailSender(),
            CreateConfiguration());

        var accounts = await service.GetAllAsync(userId, new DateOnly(2026, 3, 1));

        var bankAccount = accounts.Single(account => account.Id == bankAccountId);
        bankAccount.BalanceAtPeriodStart.Should().Be(1150m);
        bankAccount.CurrentBalance.Should().Be(1430m);

        var savingsAccount = accounts.Single(account => account.Id == transferTargetId);
        savingsAccount.BalanceAtPeriodStart.Should().Be(600m);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://example.test"
        })
        .Build();

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(AppEmailMessage message, CancellationToken ct = default) => Task.CompletedTask;
    }
}
