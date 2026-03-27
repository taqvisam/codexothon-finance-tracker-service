namespace PersonalFinanceTracker.Application.DTOs.Onboarding;

public record OnboardingImportRequest(
    IReadOnlyList<OnboardingImportAccountRow> Accounts,
    IReadOnlyList<OnboardingImportBudgetRow> Budgets,
    IReadOnlyList<OnboardingImportGoalRow> Goals,
    IReadOnlyList<OnboardingImportTransactionRow> Transactions);

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

public record OnboardingImportResponse(
    int AccountsCreated,
    int CategoriesCreated,
    int BudgetsCreated,
    int GoalsCreated,
    int TransactionsCreated);
