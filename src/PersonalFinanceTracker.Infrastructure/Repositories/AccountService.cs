using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Accounts;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class AccountService(AppDbContext dbContext) : IAccountService
{
    public async Task<IReadOnlyList<AccountResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new AccountResponse(x.Id, x.Name, x.Type, x.OpeningBalance, x.CurrentBalance, x.InstitutionName))
            .ToListAsync(ct);
    }

    public async Task<AccountResponse> CreateAsync(Guid userId, AccountRequest request, CancellationToken ct = default)
    {
        var account = new Account
        {
            UserId = userId,
            Name = request.Name,
            Type = request.Type,
            OpeningBalance = request.OpeningBalance,
            CurrentBalance = request.OpeningBalance,
            InstitutionName = request.InstitutionName
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(ct);

        return new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance, account.CurrentBalance, account.InstitutionName);
    }

    public async Task<AccountResponse> UpdateAsync(Guid userId, Guid id, AccountRequest request, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Account not found.", 404);

        account.Name = request.Name;
        account.Type = request.Type;
        account.InstitutionName = request.InstitutionName;
        account.LastUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return new AccountResponse(account.Id, account.Name, account.Type, account.OpeningBalance, account.CurrentBalance, account.InstitutionName);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Account not found.", 404);

        var hasTransactions = await dbContext.Transactions.AnyAsync(
            x => x.UserId == userId && (x.AccountId == id || x.TransferAccountId == id),
            ct
        );
        if (hasTransactions)
        {
            throw new AppException("Cannot delete account with existing transactions.");
        }

        var hasRecurring = await dbContext.RecurringTransactions.AnyAsync(x => x.UserId == userId && x.AccountId == id, ct);
        if (hasRecurring)
        {
            throw new AppException("Cannot delete account linked to recurring transactions.");
        }

        var hasGoals = await dbContext.Goals.AnyAsync(x => x.UserId == userId && x.LinkedAccountId == id, ct);
        if (hasGoals)
        {
            throw new AppException("Cannot delete account linked to goals.");
        }

        dbContext.Accounts.Remove(account);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task TransferAsync(Guid userId, TransferRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new AppException("Amount must be greater than zero.");
        if (request.FromAccountId == request.ToAccountId) throw new AppException("Source and destination accounts must differ.");

        var from = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.FromAccountId && x.UserId == userId, ct)
            ?? throw new AppException("Source account not found.", 404);
        var to = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.ToAccountId && x.UserId == userId, ct)
            ?? throw new AppException("Destination account not found.", 404);

        if (from.CurrentBalance < request.Amount)
        {
            throw new AppException("Insufficient balance.");
        }

        from.CurrentBalance -= request.Amount;
        to.CurrentBalance += request.Amount;
        from.LastUpdatedAt = DateTime.UtcNow;
        to.LastUpdatedAt = DateTime.UtcNow;

        dbContext.Transactions.Add(new Transaction
        {
            UserId = userId,
            AccountId = from.Id,
            TransferAccountId = to.Id,
            Type = TransactionType.Transfer,
            Amount = request.Amount,
            TransactionDate = request.Date,
            Note = request.Note
        });

        await dbContext.SaveChangesAsync(ct);
    }
}
