using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;
using PersonalFinanceTracker.Infrastructure.Repositories;

namespace UnitTests;

public class SharedAccountServiceTests
{
    [Fact]
    public async Task GetMembersAsync_Should_Return_Owner_When_No_Shared_Members_Exist()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            DisplayName = ""
        });
        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = ownerId,
            Name = "Primary",
            Type = AccountType.Bank,
            OpeningBalance = 1000,
            CurrentBalance = 1000
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var members = await service.GetMembersAsync(ownerId, accountId);

        members.Should().ContainSingle();
        members[0].UserId.Should().Be(ownerId);
        members[0].Role.Should().Be(AccountMemberRole.Owner);
        members[0].DisplayName.Should().Be("owner@example.com");
    }

    [Fact]
    public async Task GetActivityAsync_Should_Fall_Back_To_Email_When_DisplayName_Is_Missing()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        dbContext.Users.Add(new User
        {
            Id = ownerId,
            Email = "owner@example.com",
            DisplayName = ""
        });
        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = ownerId,
            Name = "Primary",
            Type = AccountType.Bank,
            OpeningBalance = 1000,
            CurrentBalance = 1000
        });
        dbContext.AccountActivities.Add(new AccountActivity
        {
            Id = activityId,
            AccountId = accountId,
            ActorUserId = ownerId,
            EntityType = "account",
            Action = "created",
            Description = "Created account Primary."
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var activity = await service.GetActivityAsync(ownerId, accountId);

        activity.Should().ContainSingle();
        activity[0].Id.Should().Be(activityId);
        activity[0].ActorName.Should().Be("owner@example.com");
        activity[0].Action.Should().Be("created");
    }

    [Fact]
    public async Task GetActivityAsync_Should_Return_Activity_For_Multiple_Actors()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User
            {
                Id = ownerId,
                Email = "owner@example.com",
                DisplayName = "Owner User"
            },
            new User
            {
                Id = memberId,
                Email = "member@example.com",
                DisplayName = "Member User"
            });
        dbContext.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = ownerId,
            Name = "Primary",
            Type = AccountType.Bank,
            OpeningBalance = 1000,
            CurrentBalance = 1000
        });
        dbContext.AccountMembers.Add(new AccountMember
        {
            AccountId = accountId,
            UserId = memberId,
            Role = AccountMemberRole.Editor
        });
        dbContext.AccountActivities.AddRange(
            new AccountActivity
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                ActorUserId = ownerId,
                EntityType = "membership",
                Action = "invited",
                Description = "Invited member@example.com as Editor."
            },
            new AccountActivity
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                ActorUserId = memberId,
                EntityType = "transaction",
                Action = "created",
                Description = "Added transaction Coffee."
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var activity = await service.GetActivityAsync(ownerId, accountId);

        activity.Should().HaveCount(2);
        activity.Select(item => item.ActorName).Should().Contain(["Owner User", "Member User"]);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static AccountService CreateService(AppDbContext dbContext)
    {
        var accessControlService = new AccessControlService(dbContext);
        var activityLogger = new AccountActivityLogger(dbContext);
        return new AccountService(dbContext, accessControlService, activityLogger);
    }
}
