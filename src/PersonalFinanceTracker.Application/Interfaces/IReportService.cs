using PersonalFinanceTracker.Application.DTOs.Reports;

namespace PersonalFinanceTracker.Application.Interfaces;

public interface IReportService
{
    Task<IReadOnlyList<CategorySpendReportItem>> GetCategorySpendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, Domain.Enums.TransactionType? type, CancellationToken ct = default);
    Task<IReadOnlyList<IncomeExpenseReportItem>> GetIncomeVsExpenseAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, Domain.Enums.TransactionType? type, CancellationToken ct = default);
    Task<IReadOnlyList<AccountBalanceTrendItem>> GetAccountBalanceTrendAsync(Guid userId, DateOnly from, DateOnly to, Guid? accountId, Guid? categoryId, Domain.Enums.TransactionType? type, CancellationToken ct = default);
}
