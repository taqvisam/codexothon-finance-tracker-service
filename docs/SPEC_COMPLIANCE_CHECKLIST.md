# Personal Finance Tracker V2 Spec Compliance Checklist

Source of truth: `Personal Finance Tracker Spec V2.pdf`

Status legend: `DONE` `EXTRA`

## 1. V2 Vision
- [x] `DONE` App moves beyond tracking into forecasting, health scoring, rules automation, shared finances, and deeper insights.
  - Evidence: backend services in `codexothon-finance-tracker-service/src/PersonalFinanceTracker.Infrastructure/Repositories`, V2 pages in `codexothon-finance-tracker-ui/src/features`

## 2. Cash Flow Forecasting
- [x] `DONE` Predict end-of-month balance.
  - Evidence: `ForecastService.GetMonthForecastAsync(...)`, `ForecastMonthResponse.ForecastedEndBalance`
- [x] `DONE` Show upcoming known expenses from recurring transactions and historical patterns.
  - Evidence: `ForecastService.GetRecurringDailyDeltasAsync(...)`, weighted weekday/weekend heuristic in `ForecastService.GetDailyProfileAsync(...)`
- [x] `DONE` Safe-to-spend indicator.
  - Evidence: `ForecastMonthResponse.SafeToSpend`, dashboard KPI in `ui/src/features/reports/DashboardPage.tsx`
- [x] `DONE` Daily projected balance graph.
  - Evidence: `GET /api/forecast/daily`, `LineChart` in `DashboardPage.tsx`
- [x] `DONE` Inputs use historical transactions, recurring transactions, and current balances.
  - Evidence: `ForecastService` queries `Transactions`, `RecurringTransactions`, and `Accounts`
- [x] `DONE` Outputs include forecasted balance and risk warnings.
  - Evidence: `ForecastMonthResponse`, `BuildWarnings(...)`
- [x] `DONE` Backend forecast service uses aggregation over recent history with a simple heuristic model.
  - Evidence: 90-day weighted heuristic and sparse-data fallback in `ForecastService.cs`
- [x] `DONE` APIs:
  - `GET /api/forecast/month`
  - `GET /api/forecast/daily`
  - Evidence: `ForecastController.cs`
- [x] `DONE` Dashboard widget "Projected Balance".
  - Evidence: `SummaryCard title="Projected Balance"` in `DashboardPage.tsx`
- [x] `DONE` Line chart from today to month end.
  - Evidence: `forecastDailyQuery` rendered in `DashboardPage.tsx`
- [x] `DONE` Sparse-data fallback for users with little history.
  - Evidence: model label `Sparse fallback average` in `ForecastService.cs`

## 3. Financial Health Score
- [x] `DONE` Single score from 0 to 100.
  - Evidence: `HealthScoreResponse.Score`
- [x] `DONE` Factors: savings rate, expense stability, budget adherence, cash buffer.
  - Evidence: `InsightsService.GetHealthScoreAsync(...)`
- [x] `DONE` Breakdown by factor.
  - Evidence: `HealthScoreResponse.Breakdown`
- [x] `DONE` Suggestions included.
  - Evidence: `HealthScoreResponse.Suggestions`, `BuildSuggestions(...)`
- [x] `DONE` API:
  - `GET /api/insights/health-score`
  - Evidence: `InsightsController.cs`
- [x] `DONE` Score card on dashboard.
  - Evidence: `DashboardPage.tsx`
- [x] `DONE` Drill-down page with explanation.
  - Evidence: `ui/src/features/reports/InsightsPage.tsx`

## 4. Rules Engine
- [x] `DONE` Users can define rules for auto-categorization, tagging, and alerts.
  - Evidence: `RuleActionType` enum and `RuleEngineService.cs`
- [x] `DONE` Rule structure contains condition and action.
  - Evidence: `Application/DTOs/Rules/RuleDtos.cs`
- [x] `DONE` Execution on transaction creation.
  - Evidence: `TransactionService.CreateAsync(...)`
- [x] `DONE` Execution on transaction import.
  - Evidence: `TransactionService.ImportAsync(...)`
- [x] `DONE` Priority ordering.
  - Evidence: `RuleService` ordering and `RuleEngineService` `.OrderBy(x => x.Priority)`
- [x] `DONE` APIs:
  - `GET /api/rules`
  - `POST /api/rules`
  - `PUT /api/rules/{id}`
  - `DELETE /api/rules/{id}`
  - Evidence: `RulesController.cs`
- [x] `DONE` UI rules builder is form-based, not JSON.
  - Evidence: `ui/src/features/rules/RulesPage.tsx`
- [x] `DONE` Rule list has enable/disable control.
  - Evidence: `RulesPage.tsx`

## 5. Shared Accounts (Family Mode)
- [x] `DONE` Roles: Owner, Editor, Viewer.
  - Evidence: `Domain/Enums/AccountMemberRole.cs`
- [x] `DONE` Invite users via email.
  - Evidence: `AccountService.InviteMemberAsync(...)`, invite modal in `SharedAccountPanel.tsx`
- [x] `DONE` Shared accounts and transactions.
  - Evidence: access checks in `AccessControlService.cs`, scoped transaction queries in `TransactionService.cs`
- [x] `DONE` Shared budgets and goals.
  - Evidence: `Budget.AccountId`, `BudgetService.cs`, `GoalService.cs`
- [x] `DONE` Activity tracking for shared operations.
  - Evidence: `AccountActivity.cs`, `AccountActivityLogger.cs`, `GetActivityAsync(...)`
- [x] `DONE` Permissions:
  - Owner full control
  - Editor add/edit transactions
  - Viewer read-only
  - Evidence: `EnsureCanEditAccountAsync(...)` gate used by transaction, budget, goal, and transfer flows
- [x] `DONE` Backend account membership service and access control layer.
  - Evidence: `AccountService.cs`, `AccessControlService.cs`
- [x] `DONE` APIs:
  - `POST /api/accounts/{id}/invite`
  - `GET /api/accounts/{id}/members`
  - `PUT /api/accounts/{id}/members/{userId}`
  - Evidence: `AccountsController.cs`
- [x] `DONE` DB table `account_members (id, account_id, user_id, role)`.
  - Evidence: migration `20260327070331_AddV2RulesSharedAccountsAndInsights.cs`
- [x] `DONE` UI "Shared with" section in account page.
  - Evidence: `SharedAccountPanel.tsx`, embedded in `AccountsPage.tsx`
- [x] `DONE` Invite modal.
  - Evidence: `SharedAccountPanel.tsx`
- [x] `DONE` Role selector.
  - Evidence: `SharedAccountPanel.tsx`

## 6. Advanced Reporting & Insights
- [x] `DONE` Category trends over time.
  - Evidence: `ReportService.GetCategoryTrendsAsync(...)`, `ReportsPage.tsx`
- [x] `DONE` Savings rate trend.
  - Evidence: `ReportService.GetSavingsRateTrendAsync(...)`, `ReportsPage.tsx`, `InsightsPage.tsx`
- [x] `DONE` Income vs expense over months.
  - Evidence: `ReportService.GetIncomeVsExpenseAsync(...)`, `ReportsPage.tsx`
- [x] `DONE` Net worth tracking.
  - Evidence: `ReportService.GetNetWorthAsync(...)`, `ReportsPage.tsx`
- [x] `DONE` Insight highlights such as increased spending and better saving.
  - Evidence: `InsightsService.GetHighlightsAsync(...)`, `InsightsPage.tsx`
- [x] `DONE` Filters: date range, account, category.
  - Evidence: filters in `ReportsPage.tsx` and `InsightsPage.tsx`
- [x] `DONE` APIs:
  - `GET /api/reports/trends`
  - `GET /api/reports/net-worth`
  - `GET /api/insights`
  - Evidence: `ReportsController.cs`, `InsightsController.cs`
- [x] `DONE` Insights page with highlight cards and comparison charts.
  - Evidence: `ui/src/features/reports/InsightsPage.tsx`

## 7. Architecture Additions
- [x] `DONE` Forecast Service.
  - Evidence: `ForecastService.cs`
- [x] `DONE` Insights Service.
  - Evidence: `InsightsService.cs`
- [x] `DONE` Rules Engine Service.
  - Evidence: `RuleService.cs`, `RuleEngineService.cs`
- [x] `DONE` Access Control Layer for shared accounts.
  - Evidence: `AccessControlService.cs`
- [x] `DONE` Updated app flow includes rules, forecast, and insights services.
  - Evidence: DI registrations in `Infrastructure/DependencyInjection.cs`

## 8. Database Additions
- [x] `DONE` Rules table `rules (id, user_id, condition_json, action_json, is_active)`.
  - Evidence: `Rule.cs`, `AppDbContext.cs`, migration `20260327070331_AddV2RulesSharedAccountsAndInsights.cs`
- [x] `DONE` Account members table `account_members (id, account_id, user_id, role)`.
  - Evidence: `AccountMember.cs`, `AppDbContext.cs`, migration `20260327070331_AddV2RulesSharedAccountsAndInsights.cs`

## 9. UI Additions
- [x] `DONE` Dashboard health score card.
  - Evidence: `DashboardPage.tsx`
- [x] `DONE` Dashboard forecast graph.
  - Evidence: `DashboardPage.tsx`
- [x] `DONE` New page: Insights.
  - Evidence: `router.tsx`, `InsightsPage.tsx`
- [x] `DONE` New page: Rules Engine.
  - Evidence: `router.tsx`, `RulesPage.tsx`
- [x] `DONE` New page: Shared Account Management.
  - Evidence: `router.tsx`, `SharedAccountsPage.tsx`

## 10. Verification Artifacts
- [x] `DONE` Backend builds successfully.
  - Evidence: `dotnet build` on `PersonalFinanceTracker.sln`
- [x] `DONE` Frontend builds successfully.
  - Evidence: `npm run build`
- [x] `DONE` Frontend tests pass.
  - Evidence: `npm test -- --run`
- [x] `DONE` Backend integration test setup no longer depends on manual database startup.
  - Evidence: `tests/IntegrationTests/TestApplicationFactory.cs`, `Program.cs`
- [x] `DONE` Automated V2 spec verifier exists.
  - Evidence: `scripts/verify-v2-spec.ps1`

## Extras
- [x] `EXTRA` Transaction CSV import path supports rule execution and alert surfacing.
  - Evidence: `TransactionService.ImportAsync(...)`, `TransactionsPage.tsx`
- [x] `EXTRA` Shared account activity feed is exposed in UI.
  - Evidence: `SharedAccountPanel.tsx`
- [x] `EXTRA` Dashboard alerts can be dismissed.
  - Evidence: `AlertBanner` usage in `DashboardPage.tsx`
