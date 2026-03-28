using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Onboarding;
using PersonalFinanceTracker.Application.DTOs.Rules;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class OnboardingImportService(AppDbContext dbContext) : IOnboardingImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OnboardingImportResponse> ImportAsync(Guid userId, OnboardingImportRequest request, CancellationToken ct = default)
    {
        if (request.Accounts.Count == 0)
        {
            throw new AppException("At least one account is required for onboarding import.", 400);
        }

        var hasExistingData =
            await dbContext.Accounts.AnyAsync(x => x.UserId == userId, ct) ||
            await dbContext.Transactions.AnyAsync(x => x.UserId == userId, ct) ||
            await dbContext.Budgets.AnyAsync(x => x.UserId == userId, ct) ||
            await dbContext.Goals.AnyAsync(x => x.UserId == userId, ct) ||
            await dbContext.RecurringTransactions.AnyAsync(x => x.UserId == userId, ct) ||
            await dbContext.Rules.AnyAsync(x => x.UserId == userId, ct);

        if (hasExistingData)
        {
            throw new AppException("Bulk onboarding import is only available before you add existing finance data.", 409);
        }

        var normalizedAccountNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var accountRow in request.Accounts)
        {
            var accountName = accountRow.Name.Trim();
            if (string.IsNullOrWhiteSpace(accountName))
            {
                throw new AppException("Account name is required in the workbook.", 400);
            }

            if (!normalizedAccountNames.Add(accountName))
            {
                throw new AppException($"Duplicate account name found in workbook: {accountName}", 400);
            }
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var accountsByName = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in request.Accounts)
        {
            var accountType = ParseAccountType(row.Type);
            var creditLimit = NormalizeCreditLimit(accountType, row.CreditLimit, row.Name);
            var account = new Account
            {
                UserId = userId,
                Name = row.Name.Trim(),
                Type = accountType,
                OpeningBalance = row.OpeningBalance,
                CurrentBalance = row.OpeningBalance,
                CreditLimit = creditLimit,
                InstitutionName = NormalizeOptional(row.InstitutionName),
                CreatedAt = now,
                LastUpdatedAt = now
            };

            dbContext.Accounts.Add(account);
            accountsByName[account.Name] = account;
        }

        var existingCategories = await dbContext.Categories
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);

        var categoriesByKey = existingCategories.ToDictionary(
            x => $"{x.Type}:{NormalizeKey(x.Name)}",
            StringComparer.OrdinalIgnoreCase);

        var createdCategoryCount = 0;
        Category EnsureCategory(string categoryName, CategoryType type)
        {
            var normalizedName = NormalizeOptional(categoryName)
                ?? throw new AppException("Category name is required in workbook.", 400);
            var key = $"{type}:{NormalizeKey(normalizedName)}";
            if (categoriesByKey.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var category = new Category
            {
                UserId = userId,
                Name = normalizedName,
                Type = type,
                Color = type == CategoryType.Income ? "#1F77E5" : "#94A3B8",
                Icon = type == CategoryType.Income ? "wallet" : "tag",
                IsArchived = false
            };

            dbContext.Categories.Add(category);
            categoriesByKey[key] = category;
            createdCategoryCount += 1;
            return category;
        }

        EnsureCategory("Others", CategoryType.Expense);
        EnsureCategory("Others", CategoryType.Income);

        foreach (var row in request.Budgets)
        {
            EnsureCategory(row.Category, CategoryType.Expense);
        }

        foreach (var row in request.Recurring.Where(x => !IsTransferType(x.Type)))
        {
            var recurringType = ParseTransactionType(row.Type);
            var categoryType = recurringType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            EnsureCategory(string.IsNullOrWhiteSpace(row.Category) ? "Others" : row.Category!, categoryType);
        }

        foreach (var row in request.Transactions.Where(x => !IsTransferType(x.Type)))
        {
            var transactionType = ParseTransactionType(row.Type);
            var categoryType = transactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            EnsureCategory(string.IsNullOrWhiteSpace(row.Category) ? "Others" : row.Category!, categoryType);
        }

        foreach (var row in request.Rules.Where(x => x.ActionType == RuleActionType.SetCategory))
        {
            var existingCategory = categoriesByKey.Values.FirstOrDefault(x =>
                string.Equals(x.Name, row.ActionValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existingCategory is null)
            {
                EnsureCategory(row.ActionValue, CategoryType.Expense);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        var budgetsCreated = 0;
        foreach (var row in request.Budgets)
        {
            if (row.Amount <= 0)
            {
                continue;
            }

            if (row.Month is < 1 or > 12)
            {
                throw new AppException($"Invalid budget month for category {row.Category}.", 400);
            }

            if (row.Year < 2000 || row.Year > 2100)
            {
                throw new AppException($"Invalid budget year for category {row.Category}.", 400);
            }

            var category = EnsureCategory(row.Category, CategoryType.Expense);
            var account = ResolveOptionalAccount(row.AccountName, accountsByName);

            dbContext.Budgets.Add(new Budget
            {
                UserId = userId,
                AccountId = account?.Id,
                CategoryId = category.Id,
                Month = row.Month,
                Year = row.Year,
                Amount = row.Amount,
                AlertThresholdPercent = row.AlertThresholdPercent is > 0 and <= 100 ? row.AlertThresholdPercent.Value : 80
            });
            budgetsCreated += 1;
        }

        var goalsCreated = 0;
        foreach (var row in request.Goals)
        {
            var goalName = NormalizeOptional(row.Name);
            if (goalName is null)
            {
                continue;
            }

            if (row.TargetAmount <= 0)
            {
                throw new AppException($"Goal {goalName} must have a target amount greater than zero.", 400);
            }

            var linkedAccount = ResolveOptionalAccount(row.LinkedAccountName, accountsByName);
            dbContext.Goals.Add(new Goal
            {
                UserId = userId,
                Name = goalName,
                TargetAmount = row.TargetAmount,
                CurrentAmount = Math.Max(0, row.CurrentAmount),
                TargetDate = row.TargetDate,
                LinkedAccountId = linkedAccount?.Id,
                Icon = NormalizeOptional(row.Icon),
                Color = NormalizeOptional(row.Color),
                Status = NormalizeOptional(row.Status) ?? "active"
            });
            goalsCreated += 1;
        }

        var recurringCreated = 0;
        foreach (var row in request.Recurring)
        {
            var recurringTitle = NormalizeOptional(row.Title);
            if (recurringTitle is null)
            {
                continue;
            }

            var recurringType = ParseTransactionType(row.Type);
            var recurringAccount = ResolveRequiredAccount(row.AccountName, accountsByName);
            Guid? recurringCategoryId = null;
            if (recurringType != TransactionType.Transfer)
            {
                var categoryType = recurringType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
                recurringCategoryId = EnsureCategory(string.IsNullOrWhiteSpace(row.Category) ? "Others" : row.Category!, categoryType).Id;
            }

            dbContext.RecurringTransactions.Add(new RecurringTransaction
            {
                UserId = userId,
                Title = recurringTitle,
                Type = recurringType,
                Amount = row.Amount,
                CategoryId = recurringCategoryId,
                AccountId = recurringAccount.Id,
                Frequency = ParseRecurringFrequency(row.Frequency),
                StartDate = row.StartDate,
                EndDate = row.EndDate,
                NextRunDate = row.NextRunDate,
                AutoCreateTransaction = row.AutoCreateTransaction,
                IsPaused = row.IsPaused
            });
            recurringCreated += 1;
        }

        var transactionsCreated = 0;
        foreach (var row in request.Transactions.OrderBy(x => x.Date))
        {
            var sourceAccount = ResolveRequiredAccount(row.AccountName, accountsByName);
            var transactionType = ParseTransactionType(row.Type);
            var transferAccount = transactionType == TransactionType.Transfer
                ? ResolveRequiredAccount(row.TransferAccountName, accountsByName)
                : null;

            Guid? categoryId = null;
            if (transactionType != TransactionType.Transfer)
            {
                var categoryType = transactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
                var category = EnsureCategory(string.IsNullOrWhiteSpace(row.Category) ? "Others" : row.Category!, categoryType);
                categoryId = category.Id;
            }

            if (row.Amount <= 0)
            {
                throw new AppException($"Transaction amount must be greater than zero for account {sourceAccount.Name}.", 400);
            }

            ApplyBalance(sourceAccount, transactionType, row.Amount);
            if (transactionType == TransactionType.Transfer && transferAccount is not null)
            {
                transferAccount.CurrentBalance += row.Amount;
                transferAccount.LastUpdatedAt = now;
            }

            dbContext.Transactions.Add(new Transaction
            {
                UserId = userId,
                AccountId = sourceAccount.Id,
                CategoryId = categoryId,
                Type = transactionType,
                Amount = row.Amount,
                TransactionDate = row.Date,
                Merchant = NormalizeOptional(row.Merchant),
                Note = NormalizeOptional(row.Note),
                PaymentMethod = NormalizeOptional(row.PaymentMethod),
                TransferAccountId = transferAccount?.Id,
                Tags = row.Tags is { Count: > 0 } ? string.Join(',', row.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())) : null
            });
            sourceAccount.LastUpdatedAt = now;
            transactionsCreated += 1;
        }

        var rulesCreated = 0;
        foreach (var row in request.Rules)
        {
            var ruleName = NormalizeOptional(row.Name);
            if (ruleName is null)
            {
                continue;
            }

            dbContext.Rules.Add(new Rule
            {
                UserId = userId,
                Name = ruleName,
                ConditionJson = JsonSerializer.Serialize(
                    new RuleConditionDto(row.ConditionField, row.ConditionOperator, row.ConditionValue.Trim()),
                    JsonOptions),
                ActionJson = JsonSerializer.Serialize(
                    new RuleActionDto(row.ActionType, row.ActionValue.Trim()),
                    JsonOptions),
                Priority = row.Priority,
                IsActive = row.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            });
            rulesCreated += 1;
        }

        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OnboardingImportResponse(
            AccountsCreated: request.Accounts.Count,
            CategoriesCreated: createdCategoryCount,
            BudgetsCreated: budgetsCreated,
            GoalsCreated: goalsCreated,
            TransactionsCreated: transactionsCreated,
            RecurringCreated: recurringCreated,
            RulesCreated: rulesCreated);
    }

    private static AccountType ParseAccountType(string value)
        => Enum.TryParse<AccountType>(NormalizeOptional(value), true, out var parsed)
            ? parsed
            : throw new AppException($"Unsupported account type: {value}", 400);

    private static TransactionType ParseTransactionType(string value)
        => Enum.TryParse<TransactionType>(NormalizeOptional(value), true, out var parsed)
            ? parsed
            : throw new AppException($"Unsupported transaction type: {value}", 400);

    private static decimal? NormalizeCreditLimit(AccountType type, decimal? creditLimit, string accountName)
    {
        if (type != AccountType.CreditCard)
        {
            return null;
        }

        if (!creditLimit.HasValue || creditLimit.Value <= 0)
        {
            throw new AppException($"Credit card account '{accountName}' must include a credit limit greater than zero.", 400);
        }

        return creditLimit.Value;
    }

    private static RecurringFrequency ParseRecurringFrequency(string value)
        => Enum.TryParse<RecurringFrequency>(NormalizeOptional(value), true, out var parsed)
            ? parsed
            : throw new AppException($"Unsupported recurring frequency: {value}", 400);

    private static bool IsTransferType(string value)
        => string.Equals(NormalizeOptional(value), TransactionType.Transfer.ToString(), StringComparison.OrdinalIgnoreCase);

    private static Account ResolveRequiredAccount(string? accountName, IReadOnlyDictionary<string, Account> accountsByName)
    {
        var normalized = NormalizeOptional(accountName)
            ?? throw new AppException("Account name is required in workbook transactions.", 400);

        return accountsByName.TryGetValue(normalized, out var account)
            ? account
            : throw new AppException($"Unknown account referenced in workbook: {normalized}", 400);
    }

    private static Account? ResolveOptionalAccount(string? accountName, IReadOnlyDictionary<string, Account> accountsByName)
    {
        var normalized = NormalizeOptional(accountName);
        if (normalized is null)
        {
            return null;
        }

        return accountsByName.TryGetValue(normalized, out var account)
            ? account
            : throw new AppException($"Unknown account referenced in workbook: {normalized}", 400);
    }

    private static void ApplyBalance(Account account, TransactionType type, decimal amount)
    {
        switch (type)
        {
            case TransactionType.Income:
                account.CurrentBalance += amount;
                break;
            case TransactionType.Expense:
            case TransactionType.Transfer:
                account.CurrentBalance -= amount;
                break;
        }
    }

    private static string NormalizeKey(string value) => value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
