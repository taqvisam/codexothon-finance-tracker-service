using FluentAssertions;
using PersonalFinanceTracker.Application.DTOs.Transactions;
using PersonalFinanceTracker.Application.Validators;
using PersonalFinanceTracker.Domain.Enums;

namespace UnitTests;

public class TransactionRequestValidatorTests
{
    [Fact]
    public void Should_Fail_When_Transfer_Has_No_Destination()
    {
        var validator = new TransactionRequestValidator();
        var request = new TransactionRequest(Guid.NewGuid(), null, TransactionType.Transfer, 100, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null, null, null);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_For_Expense()
    {
        var validator = new TransactionRequestValidator();
        var request = new TransactionRequest(Guid.NewGuid(), Guid.NewGuid(), TransactionType.Expense, 99, DateOnly.FromDateTime(DateTime.UtcNow), "Store", null, null, null, new List<string> { "weekly" });

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
