using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Onboarding;

public record OnboardingImportRequest(
    IReadOnlyList<OnboardingImportAccountRow> Accounts,
    IReadOnlyList<OnboardingImportBudgetRow> Budgets,
    IReadOnlyList<OnboardingImportGoalRow> Goals,
    IReadOnlyList<OnboardingImportTransactionRow> Transactions,
    IReadOnlyList<OnboardingImportRecurringRow> Recurring,
    IReadOnlyList<OnboardingImportRuleRow> Rules);

public record OnboardingImportAccountRow(
    string Name,
    string Type,
    decimal OpeningBalance,
    string? InstitutionName);

public record OnboardingImportBudgetRow(
    string Category,
    decimal Amount,
    int Month,
    int Year,
    int? AlertThresholdPercent,
    string? AccountName);

public record OnboardingImportGoalRow(
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateOnly? TargetDate,
    string? LinkedAccountName,
    string? Icon,
    string? Color,
    string? Status);

public record OnboardingImportTransactionRow(
    string AccountName,
    string Type,
    decimal Amount,
    DateOnly Date,
    string? Category,
    string? TransferAccountName,
    string? Merchant,
    string? Note,
    string? PaymentMethod,
    IReadOnlyList<string>? Tags);

public record OnboardingImportRecurringRow(
    string Title,
    string Type,
    decimal Amount,
    string? Category,
    string AccountName,
    string Frequency,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly NextRunDate,
    bool AutoCreateTransaction,
    bool IsPaused);

public record OnboardingImportRuleRow(
    string Name,
    RuleField ConditionField,
    RuleOperator ConditionOperator,
    string ConditionValue,
    RuleActionType ActionType,
    string ActionValue,
    int Priority,
    bool IsActive);

public record OnboardingImportResponse(
    int AccountsCreated,
    int CategoriesCreated,
    int BudgetsCreated,
    int GoalsCreated,
    int TransactionsCreated,
    int RecurringCreated,
    int RulesCreated);
