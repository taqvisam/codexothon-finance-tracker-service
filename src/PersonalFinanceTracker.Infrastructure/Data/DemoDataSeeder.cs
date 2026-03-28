using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Infrastructure.Data;

public static class DemoDataSeeder
{
    private const string DemoEmail = "demo@finance.com";
    private const string DemoPassword = "Demo@123";
    private const string SecondaryEmail = "test@finance.com";
    private const string SecondaryPassword = "Test@123";

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken ct = default)
    {
        // If database already has user data, skip reseeding to avoid overwriting existing records.
        if (await dbContext.Users.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentMonth = today.Month;
        var currentYear = today.Year;

        var demoUser = await EnsureUserAsync(dbContext, DemoEmail, "Demo User", DemoPassword, ct);
        var secondaryUser = await EnsureUserAsync(dbContext, SecondaryEmail, "Secondary User", SecondaryPassword, ct);

        await SeedDemoUserDataAsync(dbContext, demoUser, today, currentMonth, currentYear, now, ct);
        await EnsureSecondaryUserBaseDataAsync(dbContext, secondaryUser, currentMonth, currentYear, now, ct);
    }

    private static async Task<User> EnsureUserAsync(
        AppDbContext dbContext,
        string email,
        string displayName,
        string password,
        CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, ct);
        if (user is null)
        {
            user = new User
            {
                Email = normalizedEmail,
                DisplayName = displayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);
        }
        else
        {
            user.DisplayName = displayName;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            user.ResetPasswordTokenHash = null;
            user.ResetPasswordTokenExpiresAt = null;
            await dbContext.SaveChangesAsync(ct);
        }

        return user;
    }

    private static async Task SeedDemoUserDataAsync(
        AppDbContext dbContext,
        User user,
        DateOnly today,
        int currentMonth,
        int currentYear,
        DateTime now,
        CancellationToken ct)
    {
        var hdfcAccount = new Account
        {
            UserId = user.Id,
            Name = "HDFC Bank",
            Type = AccountType.Bank,
            OpeningBalance = 50000,
            CurrentBalance = 50000,
            InstitutionName = "HDFC",
            CreatedAt = now,
            LastUpdatedAt = now
        };
        var creditCardAccount = new Account
        {
            UserId = user.Id,
            Name = "Credit Card",
            Type = AccountType.CreditCard,
            OpeningBalance = 0,
            CurrentBalance = 0,
            CreditLimit = 60000,
            InstitutionName = "HDFC Credit",
            CreatedAt = now,
            LastUpdatedAt = now
        };
        var cashWalletAccount = new Account
        {
            UserId = user.Id,
            Name = "Cash Wallet",
            Type = AccountType.CashWallet,
            OpeningBalance = 5000,
            CurrentBalance = 5000,
            InstitutionName = "Cash",
            CreatedAt = now,
            LastUpdatedAt = now
        };

        var accounts = new List<Account> { hdfcAccount, creditCardAccount, cashWalletAccount };
        dbContext.Accounts.AddRange(accounts);

        var categoryMap = new Dictionary<string, Category>
        {
            ["Food"] = CreateCategory(user.Id, "Food", CategoryType.Expense, "#F2994A", "utensils"),
            ["Rent"] = CreateCategory(user.Id, "Rent", CategoryType.Expense, "#EB5757", "home"),
            ["Utilities"] = CreateCategory(user.Id, "Utilities", CategoryType.Expense, "#5B8DEF", "bolt"),
            ["Transport"] = CreateCategory(user.Id, "Transport", CategoryType.Expense, "#2EA05F", "car"),
            ["Shopping"] = CreateCategory(user.Id, "Shopping", CategoryType.Expense, "#A855F7", "bag"),
            ["Salary"] = CreateCategory(user.Id, "Salary", CategoryType.Income, "#1F77E5", "wallet"),
            ["Entertainment"] = CreateCategory(user.Id, "Entertainment", CategoryType.Expense, "#FF8A4C", "film")
        };
        dbContext.Categories.AddRange(categoryMap.Values);
        await dbContext.SaveChangesAsync(ct);

        var transactions = new List<Transaction>
        {
            // Income
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Salary"].Id, TransactionType.Income, 80000, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 1)), "Company Payroll", "Monthly salary"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Salary"].Id, TransactionType.Income, 12000, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 15)), "Freelance Client", "Freelance payout"),

            // Rent and utilities
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Rent"].Id, TransactionType.Expense, 20000, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 2)), "Landlord", "Monthly rent"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Utilities"].Id, TransactionType.Expense, 2800, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 5)), "Electricity Board", "Electricity bill"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Utilities"].Id, TransactionType.Expense, 1500, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 7)), "Internet Provider", "Fiber internet"),
            CreateTxn(user.Id, creditCardAccount.Id, categoryMap["Utilities"].Id, TransactionType.Expense, 700, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 10)), "Mobile Recharge", "Phone plan"),

            // Food
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 3000, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 3)), "BigBasket", "Groceries"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 1800, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 6)), "Fresh Mart", "Groceries"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 1200, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 11)), "Swiggy", "Lunch order"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 900, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 14)), "Cafe Coffee Day", "Snacks"),
            CreateTxn(user.Id, creditCardAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 1100, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 18)), "Zomato", "Dinner"),
            CreateTxn(user.Id, cashWalletAccount.Id, categoryMap["Food"].Id, TransactionType.Expense, 400, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 19)), "Street Food", "Quick bite"),

            // Transport
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Transport"].Id, TransactionType.Expense, 500, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 4)), "Uber", "Office commute"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Transport"].Id, TransactionType.Expense, 450, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 8)), "Metro Card", "Metro recharge"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Transport"].Id, TransactionType.Expense, 700, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 12)), "Ola", "Airport drop"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Transport"].Id, TransactionType.Expense, 600, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 16)), "Petrol Pump", "Fuel"),
            CreateTxn(user.Id, cashWalletAccount.Id, categoryMap["Transport"].Id, TransactionType.Expense, 350, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 21)), "Auto Rickshaw", "Local travel"),

            // Shopping
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Shopping"].Id, TransactionType.Expense, 2500, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 9)), "Amazon", "Home essentials"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Shopping"].Id, TransactionType.Expense, 1800, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 13)), "Myntra", "Clothing"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Shopping"].Id, TransactionType.Expense, 900, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 17)), "Local Store", "Accessories"),

            // Entertainment
            CreateTxn(user.Id, creditCardAccount.Id, categoryMap["Entertainment"].Id, TransactionType.Expense, 499, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 5)), "Netflix", "Monthly subscription"),
            CreateTxn(user.Id, creditCardAccount.Id, categoryMap["Entertainment"].Id, TransactionType.Expense, 900, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 15)), "PVR Cinemas", "Movie"),
            CreateTxn(user.Id, cashWalletAccount.Id, categoryMap["Entertainment"].Id, TransactionType.Expense, 300, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 20)), "Arcade", "Weekend"),
            CreateTxn(user.Id, hdfcAccount.Id, categoryMap["Entertainment"].Id, TransactionType.Expense, 600, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 22)), "BookMyShow", "Concert"),
            CreateTxn(user.Id, creditCardAccount.Id, categoryMap["Entertainment"].Id, TransactionType.Expense, 800, DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 24)), "PlayStation Store", "Game top-up")
        };
        dbContext.Transactions.AddRange(transactions);

        // Recalculate account balances from opening balances + transaction deltas.
        foreach (var account in accounts)
        {
            var delta = transactions
                .Where(x => x.AccountId == account.Id)
                .Sum(x => x.Type == TransactionType.Income ? x.Amount : -x.Amount);
            account.CurrentBalance = account.OpeningBalance + delta;
            account.LastUpdatedAt = now;
        }

        var budgets = new List<Budget>
        {
            new()
            {
                UserId = user.Id,
                CategoryId = categoryMap["Food"].Id,
                Month = currentMonth,
                Year = currentYear,
                Amount = 8000,
                AlertThresholdPercent = 80
            },
            new()
            {
                UserId = user.Id,
                CategoryId = categoryMap["Transport"].Id,
                Month = currentMonth,
                Year = currentYear,
                Amount = 3000,
                AlertThresholdPercent = 80
            },
            new()
            {
                UserId = user.Id,
                CategoryId = categoryMap["Shopping"].Id,
                Month = currentMonth,
                Year = currentYear,
                Amount = 5000,
                AlertThresholdPercent = 80
            },
            new()
            {
                UserId = user.Id,
                CategoryId = categoryMap["Entertainment"].Id,
                Month = currentMonth,
                Year = currentYear,
                Amount = 2000,
                AlertThresholdPercent = 80
            }
        };
        dbContext.Budgets.AddRange(budgets);

        var goals = new List<Goal>
        {
            new()
            {
                UserId = user.Id,
                Name = "Emergency Fund",
                TargetAmount = 100000,
                CurrentAmount = 40000,
                TargetDate = today.AddYears(1),
                Status = "active"
            },
            new()
            {
                UserId = user.Id,
                Name = "Vacation",
                TargetAmount = 50000,
                CurrentAmount = 20000,
                TargetDate = today.AddMonths(8),
                Status = "active"
            }
        };
        dbContext.Goals.AddRange(goals);

        var recurring = new List<RecurringTransaction>
        {
            new()
            {
                UserId = user.Id,
                Title = "Netflix",
                Type = TransactionType.Expense,
                Amount = 499,
                CategoryId = categoryMap["Entertainment"].Id,
                AccountId = creditCardAccount.Id,
                Frequency = RecurringFrequency.Monthly,
                StartDate = today.AddMonths(-6),
                NextRunDate = DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 25)),
                AutoCreateTransaction = true,
                IsPaused = false
            },
            new()
            {
                UserId = user.Id,
                Title = "Rent",
                Type = TransactionType.Expense,
                Amount = 20000,
                CategoryId = categoryMap["Rent"].Id,
                AccountId = hdfcAccount.Id,
                Frequency = RecurringFrequency.Monthly,
                StartDate = today.AddMonths(-8),
                NextRunDate = DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, 28)),
                AutoCreateTransaction = true,
                IsPaused = false
            },
            new()
            {
                UserId = user.Id,
                Title = "Salary",
                Type = TransactionType.Income,
                Amount = 80000,
                CategoryId = categoryMap["Salary"].Id,
                AccountId = hdfcAccount.Id,
                Frequency = RecurringFrequency.Monthly,
                StartDate = today.AddMonths(-12),
                NextRunDate = DateOnly.FromDateTime(new DateTime(currentYear, currentMonth, Math.Min(30, DateTime.DaysInMonth(currentYear, currentMonth)))),
                AutoCreateTransaction = true,
                IsPaused = false
            }
        };
        dbContext.RecurringTransactions.AddRange(recurring);

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task EnsureSecondaryUserBaseDataAsync(
        AppDbContext dbContext,
        User user,
        int currentMonth,
        int currentYear,
        DateTime now,
        CancellationToken ct)
    {
        var hasAnyAccounts = await dbContext.Accounts.AnyAsync(x => x.UserId == user.Id, ct);
        if (hasAnyAccounts)
        {
            return;
        }

        var account = new Account
        {
            UserId = user.Id,
            Name = "Test Wallet",
            Type = AccountType.CashWallet,
            OpeningBalance = 2000,
            CurrentBalance = 2000,
            InstitutionName = "Cash",
            CreatedAt = now,
            LastUpdatedAt = now
        };

        var salary = CreateCategory(user.Id, "Salary", CategoryType.Income, "#1F77E5", "wallet");
        var food = CreateCategory(user.Id, "Food", CategoryType.Expense, "#F2994A", "utensils");

        dbContext.Accounts.Add(account);
        dbContext.Categories.AddRange(salary, food);
        await dbContext.SaveChangesAsync(ct);

        dbContext.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            AccountId = account.Id,
            CategoryId = salary.Id,
            Type = TransactionType.Income,
            Amount = 2500,
            TransactionDate = new DateOnly(currentYear, currentMonth, 1),
            Merchant = "Seed Income",
            Note = "Secondary user starter data",
            CreatedAt = now,
            UpdatedAt = now
        });

        account.CurrentBalance = 4500;
        await dbContext.SaveChangesAsync(ct);
    }

    private static Category CreateCategory(Guid userId, string name, CategoryType type, string color, string icon)
    {
        return new Category
        {
            UserId = userId,
            Name = name,
            Type = type,
            Color = color,
            Icon = icon,
            IsArchived = false
        };
    }

    private static Transaction CreateTxn(
        Guid userId,
        Guid accountId,
        Guid categoryId,
        TransactionType type,
        decimal amount,
        DateOnly date,
        string merchant,
        string note)
    {
        return new Transaction
        {
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Type = type,
            Amount = amount,
            TransactionDate = date,
            Merchant = merchant,
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
