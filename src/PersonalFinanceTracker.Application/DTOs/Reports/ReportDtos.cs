namespace PersonalFinanceTracker.Application.DTOs.Reports;

public record CategorySpendReportItem(string Category, decimal Amount);
public record IncomeExpenseReportItem(string Month, decimal Income, decimal Expense);
public record AccountBalanceTrendItem(string Date, string AccountName, decimal Balance);
public record CategoryTrendReportItem(string Period, string Category, decimal Amount);
public record SavingsRateTrendReportItem(string Period, decimal SavingsRate);
public record NetWorthReportItem(string Period, decimal NetWorth);
