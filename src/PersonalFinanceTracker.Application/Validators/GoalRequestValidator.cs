using FluentValidation;
using PersonalFinanceTracker.Application.DTOs.Goals;

namespace PersonalFinanceTracker.Application.Validators;

public class GoalRequestValidator : AbstractValidator<GoalRequest>
{
    public GoalRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TargetAmount).GreaterThan(0);
    }
}
