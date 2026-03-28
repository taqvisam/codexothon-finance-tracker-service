using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PersonalFinanceTracker.Application.DTOs.Accounts;
using PersonalFinanceTracker.Application.DTOs.Transactions;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class CreditCardAccountTests
{
    [Fact]
    public async Task CreateAsync_Should_Require_CreditLimit_For_CreditCard_Accounts()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var service = CreateAccountService(dbContext);

        var act = () => service.CreateAsync(
            userId,
            new AccountRequest("Travel Card", AccountType.CreditCard, -1500m, null, "Axis"));

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.Message.Should().Be("Credit limit is required for credit card accounts.");
        exception.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_AvailableCredit_For_CreditCard_Account()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var service = CreateAccountService(dbContext);

        var response = await service.CreateAsync(
            userId,
            new AccountRequest("Travel Card", AccountType.CreditCard, -5000m, 75000m, "Axis"));

        response.CreditLimit.Should().Be(75000m);
        response.AvailableCredit.Should().Be(70000m);
    }

    [Fact]
    public async Task CreateAsync_Should_Reject_Expense_When_CreditCard_Exceeds_AvailableCredit()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var categoryId = await SeedExpenseCategoryAsync(dbContext, userId, "Food");
        var creditCardId = await SeedAccountAsync(dbContext, userId, "Travel Card", AccountType.CreditCard, -10000m, 30000m);

        var service = CreateTransactionService(dbContext);

        var act = () => service.CreateAsync(
            userId,
            new TransactionRequest(
                creditCardId,
                categoryId,
                TransactionType.Expense,
                25000m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Airline",
                "Vacation booking",
                "Credit Card",
                null,
                []));

        var exception = await Assert.ThrowsAsync<AppException>(act);
        exception.Message.Should().Be("Credit limit exceeded.");
    }

    [Fact]
    public async Task CreditCard_BillPayment_Should_Restore_AvailableCredit()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedUserAsync(dbContext);
        var categoryId = await SeedExpenseCategoryAsync(dbContext, userId, "Food");
        var bankAccountId = await SeedAccountAsync(dbContext, userId, "Checking", AccountType.Bank, 100000m);
        var creditCardId = await SeedAccountAsync(dbContext, userId, "Travel Card", AccountType.CreditCard, -5000m, 50000m);

        var transactionService = CreateTransactionService(dbContext);
        var accountService = CreateAccountService(dbContext);

        await transactionService.CreateAsync(
            userId,
            new TransactionRequest(
                creditCardId,
                categoryId,
                TransactionType.Expense,
                10000m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Hotel Booking",
                "Business trip hotel",
                "Credit Card",
                null,
                []));

        await accountService.TransferAsync(
            userId,
            new TransferRequest(
                bankAccountId,
                creditCardId,
                7000m,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Card bill payment"));

        var updatedCard = (await accountService.GetAllAsync(userId))
            .Single(account => account.Id == creditCardId);

        updatedCard.CurrentBalance.Should().Be(-8000m);
        updatedCard.CreditLimit.Should().Be(50000m);
        updatedCard.AvailableCredit.Should().Be(42000m);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AccountService CreateAccountService(AppDbContext dbContext)
    {
        var accessControlService = new AccessControlService(dbContext);
        var activityLogger = new AccountActivityLogger(dbContext);
        return new AccountService(dbContext, accessControlService, activityLogger);
    }

    private static TransactionService CreateTransactionService(AppDbContext dbContext)
    {
        var accessControlService = new AccessControlService(dbContext);
        var activityLogger = new AccountActivityLogger(dbContext);
        var ruleEngine = new Mock<IRuleEngineService>();
        ruleEngine
            .Setup(service => service.ApplyAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<TransactionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, TransactionRequest request, CancellationToken _) => new TransactionResponse(
                Guid.Empty,
                request.AccountId,
                request.CategoryId,
                request.Type,
                request.Amount,
                request.Date,
                request.Merchant,
                request.Note,
                request.PaymentMethod,
                request.TransferAccountId,
                request.Tags ?? [],
                []));

        return new TransactionService(dbContext, accessControlService, ruleEngine.Object, activityLogger);
    }

    private static async Task<Guid> SeedUserAsync(AppDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"credit-tests-{Guid.NewGuid():N}@example.com",
            DisplayName = "Credit Test User",
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
        decimal? creditLimit = null)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = type,
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            CreditLimit = creditLimit
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> SeedExpenseCategoryAsync(AppDbContext dbContext, Guid userId, string name)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Type = CategoryType.Expense
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();
        return category.Id;
    }
}
