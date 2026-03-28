using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<AccountMember> AccountMembers => Set<AccountMember>();
    public DbSet<AccountActivity> AccountActivities => Set<AccountActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(255).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(120);
            e.Property(x => x.PhoneNumber).HasMaxLength(30);
            e.Property(x => x.ProfileImageUrl);
            e.Property(x => x.IsSoftDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.OpeningBalance).HasPrecision(12, 2);
            e.Property(x => x.CurrentBalance).HasPrecision(12, 2);
            e.Property(x => x.CreditLimit).HasPrecision(12, 2);
            e.Property(x => x.InstitutionName).HasMaxLength(120);
            e.HasIndex(x => x.UserId).HasDatabaseName("ix_accounts_user_id");
        });

        modelBuilder.Entity<AccountMember>(e =>
        {
            e.ToTable("account_members");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.AccountId, x.UserId }).IsUnique().HasDatabaseName("ix_account_members_account_user");
        });

        modelBuilder.Entity<AccountActivity>(e =>
        {
            e.ToTable("account_activities");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(400).IsRequired();
            e.HasIndex(x => x.AccountId).HasDatabaseName("ix_account_activities_account_id");
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Icon).HasMaxLength(50);
            e.HasIndex(x => x.UserId).HasDatabaseName("ix_categories_user_id");
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Merchant).HasMaxLength(200);
            e.Property(x => x.PaymentMethod).HasMaxLength(50);
            e.HasIndex(x => x.UserId).HasDatabaseName("ix_transactions_user_id");
            e.HasIndex(x => x.TransactionDate).HasDatabaseName("ix_transactions_transaction_date");
        });

        modelBuilder.Entity<Budget>(e =>
        {
            e.ToTable("budgets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasIndex(x => new { x.UserId, x.AccountId, x.CategoryId, x.Month, x.Year }).IsUnique().HasDatabaseName("ix_budgets_user_account_category_month_year");
        });

        modelBuilder.Entity<Goal>(e =>
        {
            e.ToTable("goals");
            e.HasKey(x => x.Id);
            e.Property(x => x.TargetAmount).HasPrecision(12, 2);
            e.Property(x => x.CurrentAmount).HasPrecision(12, 2);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30);
            e.HasIndex(x => x.UserId).HasDatabaseName("ix_goals_user_id");
        });

        modelBuilder.Entity<RecurringTransaction>(e =>
        {
            e.ToTable("recurring_transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Amount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Rule>(e =>
        {
            e.ToTable("rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ConditionJson).IsRequired();
            e.Property(x => x.ActionJson).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Priority }).HasDatabaseName("ix_rules_user_priority");
        });
    }
}
