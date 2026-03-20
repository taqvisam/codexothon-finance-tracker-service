using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Transactions;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Application.Validators;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class TransactionService(AppDbContext dbContext) : ITransactionService
{
    private readonly TransactionRequestValidator _validator = new();

    public async Task<IReadOnlyList<TransactionResponse>> GetAllAsync(
        Guid userId,
        DateOnly? from,
        DateOnly? to,
        Guid? accountId,
        Guid? categoryId,
        TransactionType? type,
        decimal? minAmount,
        decimal? maxAmount,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = dbContext.Transactions.Where(x => x.UserId == userId);
        if (from.HasValue) query = query.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.TransactionDate <= to.Value);
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        if (minAmount.HasValue) query = query.Where(x => x.Amount >= minAmount.Value);
        if (maxAmount.HasValue) query = query.Where(x => x.Amount <= maxAmount.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Merchant != null && x.Merchant.ToLower().Contains(term)) ||
                (x.Note != null && x.Note.ToLower().Contains(term)));
        }

        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

        var rows = await query.OrderByDescending(x => x.TransactionDate)
            .Select(x => new { x.Id, x.AccountId, x.CategoryId, x.Type, x.Amount, x.TransactionDate, x.Merchant, x.Note, x.PaymentMethod, x.TransferAccountId, x.Tags })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return rows.Select(x => new TransactionResponse(x.Id, x.AccountId, x.CategoryId, x.Type, x.Amount, x.TransactionDate, x.Merchant, x.Note, x.PaymentMethod, x.TransferAccountId, string.IsNullOrWhiteSpace(x.Tags) ? new List<string>() : x.Tags.Split(',').ToList())).ToList();
    }

    public async Task<TransactionResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var row = await dbContext.Transactions
            .Where(x => x.UserId == userId && x.Id == id)
            .Select(x => new { x.Id, x.AccountId, x.CategoryId, x.Type, x.Amount, x.TransactionDate, x.Merchant, x.Note, x.PaymentMethod, x.TransferAccountId, x.Tags })
            .FirstOrDefaultAsync(ct) ?? throw new AppException("Transaction not found.", 404);

        return new TransactionResponse(
            row.Id,
            row.AccountId,
            row.CategoryId,
            row.Type,
            row.Amount,
            row.TransactionDate,
            row.Merchant,
            row.Note,
            row.PaymentMethod,
            row.TransferAccountId,
            string.IsNullOrWhiteSpace(row.Tags) ? new List<string>() : row.Tags.Split(',').ToList());
    }

    public async Task<TransactionResponse> CreateAsync(Guid userId, TransactionRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        ValidateTransferRequest(request);

        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId && x.UserId == userId, ct)
            ?? throw new AppException("Account not found.", 404);

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == request.CategoryId && x.UserId == userId, ct);
            if (!categoryExists) throw new AppException("Category not found.", 404);
        }

        EnsureSufficientBalance(account, request.Type, request.Amount);
        ApplyBalance(account, request.Type, request.Amount);

        if (request.Type == TransactionType.Transfer && request.TransferAccountId.HasValue)
        {
            var dest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.TransferAccountId && x.UserId == userId, ct)
                ?? throw new AppException("Destination account not found.", 404);
            dest.CurrentBalance += request.Amount;
            dest.LastUpdatedAt = DateTime.UtcNow;
        }

        var entity = new Transaction
        {
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.Date,
            Merchant = request.Merchant,
            Note = request.Note,
            PaymentMethod = request.PaymentMethod,
            TransferAccountId = request.TransferAccountId,
            Tags = request.Tags is { Count: > 0 } ? string.Join(',', request.Tags) : null
        };

        account.LastUpdatedAt = DateTime.UtcNow;
        dbContext.Transactions.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return new TransactionResponse(entity.Id, entity.AccountId, entity.CategoryId, entity.Type, entity.Amount, entity.TransactionDate, entity.Merchant, entity.Note, entity.PaymentMethod, entity.TransferAccountId, request.Tags ?? new());
    }

    public async Task<TransactionResponse> UpdateAsync(Guid userId, Guid id, TransactionRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        ValidateTransferRequest(request);

        var existing = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Transaction not found.", 404);

        var oldAccount = await dbContext.Accounts.FirstAsync(x => x.Id == existing.AccountId && x.UserId == userId, ct);
        RevertBalance(oldAccount, existing.Type, existing.Amount);
        if (existing.Type == TransactionType.Transfer && existing.TransferAccountId.HasValue)
        {
            var oldDest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == existing.TransferAccountId && x.UserId == userId, ct);
            if (oldDest is not null)
            {
                oldDest.CurrentBalance -= existing.Amount;
                oldDest.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        var newAccount = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId && x.UserId == userId, ct)
            ?? throw new AppException("Account not found.", 404);
        EnsureSufficientBalance(newAccount, request.Type, request.Amount);
        ApplyBalance(newAccount, request.Type, request.Amount);
        if (request.Type == TransactionType.Transfer && request.TransferAccountId.HasValue)
        {
            var newDest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.TransferAccountId && x.UserId == userId, ct)
                ?? throw new AppException("Destination account not found.", 404);
            newDest.CurrentBalance += request.Amount;
            newDest.LastUpdatedAt = DateTime.UtcNow;
        }

        existing.AccountId = request.AccountId;
        existing.CategoryId = request.CategoryId;
        existing.Type = request.Type;
        existing.Amount = request.Amount;
        existing.TransactionDate = request.Date;
        existing.Merchant = request.Merchant;
        existing.Note = request.Note;
        existing.PaymentMethod = request.PaymentMethod;
        existing.TransferAccountId = request.TransferAccountId;
        existing.Tags = request.Tags is { Count: > 0 } ? string.Join(',', request.Tags) : null;
        existing.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return new TransactionResponse(existing.Id, existing.AccountId, existing.CategoryId, existing.Type, existing.Amount, existing.TransactionDate, existing.Merchant, existing.Note, existing.PaymentMethod, existing.TransferAccountId, request.Tags ?? new());
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var existing = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Transaction not found.", 404);
        var account = await dbContext.Accounts.FirstAsync(x => x.Id == existing.AccountId && x.UserId == userId, ct);
        RevertBalance(account, existing.Type, existing.Amount);
        if (existing.Type == TransactionType.Transfer && existing.TransferAccountId.HasValue)
        {
            var dest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == existing.TransferAccountId && x.UserId == userId, ct);
            if (dest is not null)
            {
                dest.CurrentBalance -= existing.Amount;
                dest.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        dbContext.Transactions.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
    }

    private static void ApplyBalance(Account account, TransactionType type, decimal amount)
    {
        if (amount <= 0) throw new AppException("Amount must be greater than zero.");
        account.CurrentBalance += type == TransactionType.Income ? amount : -amount;
    }

    private static void RevertBalance(Account account, TransactionType type, decimal amount)
    {
        account.CurrentBalance += type == TransactionType.Income ? -amount : amount;
    }

    private static void EnsureSufficientBalance(Account account, TransactionType type, decimal amount)
    {
        var debitsAccount = type is TransactionType.Expense or TransactionType.Transfer;
        if (debitsAccount && account.CurrentBalance < amount)
        {
            throw new AppException("Insufficient balance.");
        }
    }

    private static void ValidateTransferRequest(TransactionRequest request)
    {
        if (request.Type != TransactionType.Transfer)
        {
            return;
        }

        if (!request.TransferAccountId.HasValue)
        {
            throw new AppException("Destination account is required for transfer.");
        }

        if (request.TransferAccountId.Value == request.AccountId)
        {
            throw new AppException("Source and destination accounts must differ.");
        }
    }
}

