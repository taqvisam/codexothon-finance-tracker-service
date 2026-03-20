using FluentValidation;
using PersonalFinanceTracker.Application.DTOs.Transactions;

namespace PersonalFinanceTracker.Application.Validators;

public class TransactionRequestValidator : AbstractValidator<TransactionRequest>
{
    public TransactionRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.AccountId).NotEqual(Guid.Empty);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x).Must(x => x.Type == Domain.Enums.TransactionType.Transfer ? x.TransferAccountId.HasValue : true)
            .WithMessage("Transfer requires destination account.");
        RuleFor(x => x).Must(x => x.Type == Domain.Enums.TransactionType.Transfer || x.CategoryId.HasValue)
            .WithMessage("Category is required except transfer.");
    }
}
