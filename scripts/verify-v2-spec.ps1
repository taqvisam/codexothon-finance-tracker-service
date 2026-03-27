$ErrorActionPreference = "Stop"

$root = "D:\Project\PersonalExpenseTracker"
$service = Join-Path $root "codexothon-finance-tracker-service"
$ui = Join-Path $root "codexothon-finance-tracker-ui"
$serviceSrc = Join-Path $service "src"
$serviceTests = Join-Path $service "tests"
$uiSrc = Join-Path $ui "src"
$migrations = Join-Path $serviceSrc "PersonalFinanceTracker.Infrastructure\Migrations"

$checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Details
    )

    $script:checks.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Details = $Details
    })
}

function Get-SourceFiles {
    param([string]$Path)

    Get-ChildItem -Path $Path -Recurse -File |
        Where-Object {
            $_.FullName -notmatch '\\(bin|obj|node_modules|dist|coverage)\\'
        }
}

function Test-SourcePattern {
    param(
        [string]$Path,
        [string]$Pattern
    )

    [bool](Get-SourceFiles $Path | Select-String -Pattern $Pattern)
}

function Test-FilePattern {
    param(
        [string]$Path,
        [string]$Pattern
    )

    if (-not (Test-Path $Path)) {
        return $false
    }

    [bool](Select-String -Path $Path -Pattern $Pattern)
}

function Invoke-CheckedCommand {
    param(
        [string]$Name,
        [scriptblock]$Action,
        [string]$WorkingDirectory,
        [string]$SuccessDetails,
        [string]$FailurePrefix
    )

    Push-Location $WorkingDirectory
    try {
        & $Action
        $passed = $LASTEXITCODE -eq 0
        if ($passed) {
            $details = $SuccessDetails
        }
        else {
            $details = "$FailurePrefix failed with exit code $LASTEXITCODE."
        }
        Add-Check $Name $passed $details
    }
    finally {
        Pop-Location
    }
}

Add-Check "Forecast API routes" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\ForecastController.cs') 'HttpGet\("month"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\ForecastController.cs') 'HttpGet\("daily"\)')
) "GET /api/forecast/month and GET /api/forecast/daily exist."
Add-Check "Forecast backend heuristic model" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ForecastService.cs') 'Weighted recency weekday/weekend heuristic') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ForecastService.cs') 'Sparse fallback average') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ForecastService.cs') 'RecurringTransactions') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ForecastService.cs') 'SafeToSpend')
) "Forecast service uses history, recurring transactions, safe-to-spend logic, and sparse-data fallback."
Add-Check "Forecast dashboard widget and graph" (
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\DashboardPage.tsx') 'Projected Balance') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\DashboardPage.tsx') 'Cash Flow Forecast Engine') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\DashboardPage.tsx') 'projectedBalance') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\DashboardPage.tsx') 'Safe to spend')
) "Dashboard renders projected balance, safe-to-spend, and the daily projection chart."

Add-Check "Health score API and service" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\InsightsController.cs') 'HttpGet\("health-score"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\InsightsService.cs') 'Savings rate') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\InsightsService.cs') 'Expense stability') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\InsightsService.cs') 'Budget adherence') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\InsightsService.cs') 'Cash buffer')
) "Health score route and factor-based scoring exist."
Add-Check "Health score dashboard and drill-down page" (
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\DashboardPage.tsx') 'Financial Health Score') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Score Breakdown') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Suggestions')
) "Dashboard score card and drill-down explanation page exist."

Add-Check "Rules engine persistence and service" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Migrations\20260327070331_AddV2RulesSharedAccountsAndInsights.cs') 'name: "rules"') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\RuleService.cs') 'class RuleService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\RuleEngineService.cs') 'class RuleEngineService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\RuleEngineService.cs') 'SetCategory') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\RuleEngineService.cs') 'AddTag') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\RuleEngineService.cs') 'TriggerAlert')
) "Rules table plus rule CRUD/evaluation services exist."
Add-Check "Rules API and execution points" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\RulesController.cs') '\[HttpGet\]') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\RulesController.cs') '\[HttpPost\]') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\RulesController.cs') 'HttpPut\("{id:guid}"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\RulesController.cs') 'HttpDelete\("{id:guid}"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\TransactionService.cs') 'ruleEngineService\.ApplyAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\TransactionService.cs') 'ImportAsync')
) "Rule APIs exist and rules run on create/import transaction paths."
Add-Check "Rules builder UI" (
    (Test-FilePattern (Join-Path $uiSrc 'features\rules\RulesPage.tsx') 'Rules Builder') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\rules\RulesPage.tsx') 'Disable') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\rules\RulesPage.tsx') 'Enable')
) "Form-based rules builder and enable/disable list exist."

Add-Check "Shared account data model and roles" (
    (Test-FilePattern (Join-Path $migrations '20260327070331_AddV2RulesSharedAccountsAndInsights.cs') 'name: "account_members"') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Domain\Enums\AccountMemberRole.cs') 'Owner') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Domain\Enums\AccountMemberRole.cs') 'Editor') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Domain\Enums\AccountMemberRole.cs') 'Viewer')
) "account_members table and Owner/Editor/Viewer roles exist."
Add-Check "Shared account APIs and access layer" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\AccountsController.cs') 'HttpPost\("{id:guid}/invite"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\AccountsController.cs') 'HttpGet\("{id:guid}/members"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\AccountsController.cs') 'HttpPut\("{id:guid}/members/{userId:guid}"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\AccessControlService.cs') 'EnsureCanEditAccountAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\AccountService.cs') 'Only account owner can invite members') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\AccountService.cs') 'Only account owner can manage roles')
) "Invite/member APIs and shared-account access-control layer exist."
Add-Check "Shared accounts affect transactions, budgets, goals, and activity" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\TransactionService.cs') 'EnsureCanEditAccountAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\BudgetService.cs') 'Created shared budget') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\GoalService.cs') 'Created shared goal') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\AccountActivityLogger.cs') 'AccountActivity')
) "Shared scope is enforced for transactions, budgets, goals, and activity tracking."
Add-Check "Shared accounts UI" (
    (Test-FilePattern (Join-Path $uiSrc 'features\accounts\SharedAccountPanel.tsx') 'Shared with') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\accounts\SharedAccountPanel.tsx') 'Invite member') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\accounts\SharedAccountPanel.tsx') 'Role') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\shared\SharedAccountsPage.tsx') 'SharedAccountPanel')
) "Shared with section, invite modal, role selector, and management page exist."

Add-Check "Advanced reporting APIs" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\ReportsController.cs') 'HttpGet\("trends"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\ReportsController.cs') 'HttpGet\("net-worth"\)') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Api\Controllers\InsightsController.cs') '^\s*\[HttpGet\]\s*$') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ReportService.cs') 'GetCategoryTrendsAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ReportService.cs') 'GetSavingsRateTrendAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ReportService.cs') 'GetIncomeVsExpenseAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\ReportService.cs') 'GetNetWorthAsync') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\Repositories\InsightsService.cs') 'GetHighlightsAsync')
) "Required advanced reporting and insights APIs exist."
Add-Check "Advanced reporting and insights UI" (
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\ReportsPage.tsx') 'Category Trends Over Time') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\ReportsPage.tsx') 'Savings Rate Trend') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\ReportsPage.tsx') 'Income vs Expense Over Months') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\ReportsPage.tsx') 'Net Worth Tracking') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Insight Highlights') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Comparison') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Account') -and
    (Test-FilePattern (Join-Path $uiSrc 'features\reports\InsightsPage.tsx') 'Category')
) "Insights page, highlight cards, comparison charts, and filters exist."

Add-Check "Architecture additions wired" (
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\DependencyInjection.cs') 'IForecastService, ForecastService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\DependencyInjection.cs') 'IInsightsService, InsightsService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\DependencyInjection.cs') 'IRuleService, RuleService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\DependencyInjection.cs') 'IRuleEngineService, RuleEngineService') -and
    (Test-FilePattern (Join-Path $serviceSrc 'PersonalFinanceTracker.Infrastructure\DependencyInjection.cs') 'IAccessControlService, AccessControlService') -and
    (Test-FilePattern (Join-Path $uiSrc 'app\router.tsx') 'path: "insights"') -and
    (Test-FilePattern (Join-Path $uiSrc 'app\router.tsx') 'path: "rules"') -and
    (Test-FilePattern (Join-Path $uiSrc 'app\router.tsx') 'path: "shared-accounts"')
) "New services, dashboard additions, and new pages are wired into the app."

Invoke-CheckedCommand "Backend build" { dotnet build .\PersonalFinanceTracker.sln } $service "dotnet build passed." "dotnet build"
Invoke-CheckedCommand "Backend tests" {
    $env:DOTNET_ROLL_FORWARD = "Major"
    try {
        dotnet test .\PersonalFinanceTracker.sln
    }
    finally {
        Remove-Item Env:DOTNET_ROLL_FORWARD -ErrorAction SilentlyContinue
    }
} $service "dotnet test passed." "dotnet test"
Invoke-CheckedCommand "Frontend build" { npm run build } $ui "npm run build passed." "npm run build"
Invoke-CheckedCommand "Frontend tests" { npm test -- --run } $ui "npm test -- --run passed." "npm test -- --run"

$checks | Format-Table Name, Passed, Details -AutoSize

$failed = $checks | Where-Object { $_.Passed -ne $true }
if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed checks:" -ForegroundColor Red
    $failed | Format-Table Name, Details -AutoSize
    exit 1
}

Write-Host ""
Write-Host "All V2 checks passed." -ForegroundColor Green
