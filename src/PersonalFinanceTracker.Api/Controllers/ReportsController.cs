using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.Interfaces;
using System.Text;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("category-spend")]
    public async Task<IActionResult> CategorySpend(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] PersonalFinanceTracker.Domain.Enums.TransactionType? type,
        CancellationToken ct)
        => Ok(await reportService.GetCategorySpendAsync(User.GetUserId(), from, to, accountId, categoryId, type, ct));

    [HttpGet("income-vs-expense")]
    public async Task<IActionResult> IncomeVsExpense(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] PersonalFinanceTracker.Domain.Enums.TransactionType? type,
        CancellationToken ct)
        => Ok(await reportService.GetIncomeVsExpenseAsync(User.GetUserId(), from, to, accountId, categoryId, type, ct));

    [HttpGet("account-balance-trend")]
    public async Task<IActionResult> AccountBalanceTrend(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] PersonalFinanceTracker.Domain.Enums.TransactionType? type,
        CancellationToken ct)
        => Ok(await reportService.GetAccountBalanceTrendAsync(User.GetUserId(), from, to, accountId, categoryId, type, ct));

    [HttpGet("trends")]
    public async Task<IActionResult> Trends(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
        => Ok(new
        {
            categoryTrends = await reportService.GetCategoryTrendsAsync(User.GetUserId(), from, to, accountId, categoryId, ct),
            savingsRateTrend = await reportService.GetSavingsRateTrendAsync(User.GetUserId(), from, to, accountId, ct),
            incomeVsExpense = await reportService.GetIncomeVsExpenseAsync(User.GetUserId(), from, to, accountId, categoryId, null, ct)
        });

    [HttpGet("net-worth")]
    public async Task<IActionResult> NetWorth(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        CancellationToken ct)
        => Ok(await reportService.GetNetWorthAsync(User.GetUserId(), from, to, accountId, ct));

    [HttpGet("category-spend/export-csv")]
    public async Task<IActionResult> ExportCategorySpendCsv(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? categoryId,
        [FromQuery] PersonalFinanceTracker.Domain.Enums.TransactionType? type,
        CancellationToken ct)
    {
        var rows = await reportService.GetCategorySpendAsync(User.GetUserId(), from, to, accountId, categoryId, type, ct);
        var csv = new StringBuilder();
        csv.AppendLine("Category,Amount");
        foreach (var row in rows)
        {
            csv.AppendLine($"{row.Category},{row.Amount}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "category-spend.csv");
    }
}
