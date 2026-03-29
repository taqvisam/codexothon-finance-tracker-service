using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.DTOs.Accounts;
using PersonalFinanceTracker.Application.DTOs.Onboarding;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class AccountBalanceGuardTests
{
    [Fact]
    public async Task UpdateAsync_Should_Reject_NonCredit_Balance_Going_Negative()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var accountId = await SeedAccountAsync(dbContext, userId, "Pocket Cash", AccountType.CashWallet, 1500m, 600m);
        var service = new AccountService(
            dbContext,
            new AccessControlService(dbContext),
            new AccountActivityLogger(dbContext),
            new NoOpEmailSender(),
            CreateConfiguration());

        var act = () => service.UpdateAsync(
            userId,
            accountId,
            new AccountRequest("Pocket Cash", AccountType.CashWallet, 500m, null, "Wallet"));

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.Message.Should().Be("Balance cannot go negative for Pocket Cash.");
        exception.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ImportAsync_Should_Reject_Workbook_That_Overdraws_NonCredit_Account()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var service = new OnboardingImportService(dbContext);

        var request = new OnboardingImportRequest(
            Accounts:
            [
                new OnboardingImportAccountRow("Pocket Cash", "CashWallet", 2500m, null, "Wallet")
            ],
            Budgets: [],
            Goals: [],
            Transactions:
            [
                new OnboardingImportTransactionRow(
                    "Pocket Cash",
                    "Expense",
                    2600m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    "Transport",
                    null,
                    "Metro Card",
                    "Commute top-up",
                    "Cash",
                    [])
            ],
            Recurring: [],
            Rules: []);

        var act = () => service.ImportAsync(userId, request);

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.Message.Should().Be("Insufficient funds in Pocket Cash.");
        exception.StatusCode.Should().Be(400);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"account-guard-{Guid.NewGuid():N}@example.com",
            DisplayName = "Account Guard User",
            PasswordHash = "hash"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Guid> SeedAccountAsync(
        AppDbContext dbContext,
        Guid userId,
        string name,
        AccountType type,
        decimal openingBalance,
        decimal currentBalance)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = type,
            OpeningBalance = openingBalance,
            CurrentBalance = currentBalance
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
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
