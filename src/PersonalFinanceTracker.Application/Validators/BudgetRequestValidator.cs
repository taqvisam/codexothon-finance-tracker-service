using FluentValidation;
using PersonalFinanceTracker.Application.DTOs.Budgets;

namespace PersonalFinanceTracker.Application.Validators;

public class BudgetRequestValidator : AbstractValidator<BudgetRequest>
{
    public BudgetRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Year).GreaterThan(2000);
    }
}
