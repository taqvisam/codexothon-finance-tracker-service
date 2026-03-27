using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Rules;

public record RuleConditionDto(
    RuleField Field,
    RuleOperator Operator,
    string Value);

public record RuleActionDto(
    RuleActionType Type,
    string Value);

public record RuleRequest(
    string Name,
    RuleConditionDto Condition,
    RuleActionDto Action,
    int Priority,
    bool IsActive);

public record RuleResponse(
    Guid Id,
    string Name,
    RuleConditionDto Condition,
    RuleActionDto Action,
    int Priority,
    bool IsActive);
