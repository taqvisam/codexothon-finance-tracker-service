using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalFinanceTracker.Infrastructure.Data;

#nullable disable

namespace PersonalFinanceTracker.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260321090500_FixBudgetUniqueIndex")]
public partial class FixBudgetUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_budgets_user_month_year",
            table: "budgets");

        migrationBuilder.CreateIndex(
            name: "ix_budgets_user_category_month_year",
            table: "budgets",
            columns: new[] { "UserId", "CategoryId", "Month", "Year" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_budgets_user_category_month_year",
            table: "budgets");

        migrationBuilder.CreateIndex(
            name: "ix_budgets_user_month_year",
            table: "budgets",
            columns: new[] { "UserId", "Month", "Year" },
            unique: true);
    }
}
