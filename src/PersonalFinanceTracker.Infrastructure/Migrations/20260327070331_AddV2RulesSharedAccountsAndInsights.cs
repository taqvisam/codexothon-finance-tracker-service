using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddV2RulesSharedAccountsAndInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_budgets_user_month_year",
                table: "budgets");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "budgets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_activities_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_activities_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "account_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_account_members_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_account_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ConditionJson = table.Column<string>(type: "text", nullable: false),
                    ActionJson = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rules_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_budgets_AccountId",
                table: "budgets",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "ix_budgets_user_account_category_month_year",
                table: "budgets",
                columns: new[] { "UserId", "AccountId", "CategoryId", "Month", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_activities_account_id",
                table: "account_activities",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_account_activities_ActorUserId",
                table: "account_activities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "ix_account_members_account_user",
                table: "account_members",
                columns: new[] { "AccountId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_members_UserId",
                table: "account_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_rules_user_priority",
                table: "rules",
                columns: new[] { "UserId", "Priority" });

            migrationBuilder.AddForeignKey(
                name: "FK_budgets_accounts_AccountId",
                table: "budgets",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_budgets_accounts_AccountId",
                table: "budgets");

            migrationBuilder.DropTable(
                name: "account_activities");

            migrationBuilder.DropTable(
                name: "account_members");

            migrationBuilder.DropTable(
                name: "rules");

            migrationBuilder.DropIndex(
                name: "IX_budgets_AccountId",
                table: "budgets");

            migrationBuilder.DropIndex(
                name: "ix_budgets_user_account_category_month_year",
                table: "budgets");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "budgets");

            migrationBuilder.CreateIndex(
                name: "ix_budgets_user_month_year",
                table: "budgets",
                columns: new[] { "UserId", "Month", "Year" },
                unique: true);
        }
    }
}
