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

public class TransactionService(
    AppDbContext dbContext,
    IAccessControlService accessControlService,
    IRuleEngineService ruleEngineService,
    AccountActivityLogger activityLogger) : ITransactionService
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
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var query = dbContext.Transactions.Where(x => accessibleAccountIds.Contains(x.AccountId));
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

        return rows.Select(x => new TransactionResponse(
            x.Id,
            x.AccountId,
            x.CategoryId,
            x.Type,
            x.Amount,
            x.TransactionDate,
            x.Merchant,
            x.Note,
            x.PaymentMethod,
            x.TransferAccountId,
            string.IsNullOrWhiteSpace(x.Tags) ? new List<string>() : x.Tags.Split(',').ToList(),
            new List<string>()))
            .ToList();
    }

    public async Task<TransactionResponse> GetByIdAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var row = await dbContext.Transactions
            .Where(x => x.Id == id && accessibleAccountIds.Contains(x.AccountId))
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
            string.IsNullOrWhiteSpace(row.Tags) ? new List<string>() : row.Tags.Split(',').ToList(),
            new List<string>());
    }

    public async Task<TransactionResponse> CreateAsync(Guid userId, TransactionRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        ValidateTransferRequest(request);

        var accountAccess = await accessControlService.EnsureCanEditAccountAsync(userId, request.AccountId, ct);
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId, ct)
            ?? throw new AppException("Account not found.", 404);

        var ruleApplied = await ruleEngineService.ApplyAsync(userId, accountAccess.OwnerUserId, request, ct);
        await EnsureCategoryIsValidAsync(ruleApplied.CategoryId, accountAccess.OwnerUserId, ct);

        EnsureSufficientBalance(account, request.Type, request.Amount);
        ApplyBalance(account, request.Type, request.Amount);

        if (request.Type == TransactionType.Transfer && request.TransferAccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, request.TransferAccountId.Value, ct);
            var dest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.TransferAccountId, ct)
                ?? throw new AppException("Destination account not found.", 404);
            dest.CurrentBalance += request.Amount;
            dest.LastUpdatedAt = DateTime.UtcNow;
        }

        var entity = new Transaction
        {
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = ruleApplied.CategoryId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.Date,
            Merchant = request.Merchant,
            Note = request.Note,
            PaymentMethod = request.PaymentMethod,
            TransferAccountId = request.TransferAccountId,
            Tags = ruleApplied.Tags.Count > 0 ? string.Join(',', ruleApplied.Tags) : null
        };

        account.LastUpdatedAt = DateTime.UtcNow;
        dbContext.Transactions.Add(entity);
        activityLogger.Log(account.Id, userId, "transaction", "created", $"Added {request.Type} transaction for {request.Amount:0.##}.", entity.Id);
        await dbContext.SaveChangesAsync(ct);

        return ruleApplied with { Id = entity.Id };
    }

    public async Task<TransactionImportResponse> ImportAsync(Guid userId, TransactionImportRequest request, CancellationToken ct = default)
    {
        if (request.Items.Count == 0)
        {
            throw new AppException("At least one transaction is required for import.", 400);
        }

        var alerts = new List<string>();
        foreach (var item in request.Items)
        {
            var created = await CreateAsync(userId, item, ct);
            alerts.AddRange(created.Alerts ?? []);
        }

        return new TransactionImportResponse(request.Items.Count, alerts);
    }

    public async Task<TransactionResponse> UpdateAsync(Guid userId, Guid id, TransactionRequest request, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);
        ValidateTransferRequest(request);

        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var existing = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && accessibleAccountIds.Contains(x.AccountId), ct)
            ?? throw new AppException("Transaction not found.", 404);

        await accessControlService.EnsureCanEditAccountAsync(userId, existing.AccountId, ct);

        var oldAccount = await dbContext.Accounts.FirstAsync(x => x.Id == existing.AccountId, ct);
        RevertBalance(oldAccount, existing.Type, existing.Amount);
        if (existing.Type == TransactionType.Transfer && existing.TransferAccountId.HasValue)
        {
            var oldDest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == existing.TransferAccountId, ct);
            if (oldDest is not null)
            {
                oldDest.CurrentBalance -= existing.Amount;
                oldDest.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        var newAccountAccess = await accessControlService.EnsureCanEditAccountAsync(userId, request.AccountId, ct);
        var newAccount = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId, ct)
            ?? throw new AppException("Account not found.", 404);
        var ruleApplied = await ruleEngineService.ApplyAsync(userId, newAccountAccess.OwnerUserId, request, ct);
        await EnsureCategoryIsValidAsync(ruleApplied.CategoryId, newAccountAccess.OwnerUserId, ct);

        EnsureSufficientBalance(newAccount, request.Type, request.Amount);
        ApplyBalance(newAccount, request.Type, request.Amount);
        if (request.Type == TransactionType.Transfer && request.TransferAccountId.HasValue)
        {
            await accessControlService.EnsureCanEditAccountAsync(userId, request.TransferAccountId.Value, ct);
            var newDest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == request.TransferAccountId, ct)
                ?? throw new AppException("Destination account not found.", 404);
            newDest.CurrentBalance += request.Amount;
            newDest.LastUpdatedAt = DateTime.UtcNow;
        }

        existing.AccountId = request.AccountId;
        existing.CategoryId = ruleApplied.CategoryId;
        existing.Type = request.Type;
        existing.Amount = request.Amount;
        existing.TransactionDate = request.Date;
        existing.Merchant = request.Merchant;
        existing.Note = request.Note;
        existing.PaymentMethod = request.PaymentMethod;
        existing.TransferAccountId = request.TransferAccountId;
        existing.Tags = ruleApplied.Tags.Count > 0 ? string.Join(',', ruleApplied.Tags) : null;
        existing.UpdatedAt = DateTime.UtcNow;

        activityLogger.Log(existing.AccountId, userId, "transaction", "updated", $"Updated {request.Type} transaction.", existing.Id);
        await dbContext.SaveChangesAsync(ct);
        return ruleApplied with { Id = existing.Id };
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var accessibleAccountIds = await accessControlService.GetAccessibleAccountIdsAsync(userId, ct);
        var existing = await dbContext.Transactions.FirstOrDefaultAsync(x => x.Id == id && accessibleAccountIds.Contains(x.AccountId), ct)
            ?? throw new AppException("Transaction not found.", 404);
        await accessControlService.EnsureCanEditAccountAsync(userId, existing.AccountId, ct);

        var account = await dbContext.Accounts.FirstAsync(x => x.Id == existing.AccountId, ct);
        RevertBalance(account, existing.Type, existing.Amount);
        if (existing.Type == TransactionType.Transfer && existing.TransferAccountId.HasValue)
        {
            var dest = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Id == existing.TransferAccountId, ct);
            if (dest is not null)
            {
                dest.CurrentBalance -= existing.Amount;
                dest.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        activityLogger.Log(existing.AccountId, userId, "transaction", "deleted", "Deleted transaction.", existing.Id);
        dbContext.Transactions.Remove(existing);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task EnsureCategoryIsValidAsync(Guid? categoryId, Guid categoryOwnerId, CancellationToken ct)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var categoryExists = await dbContext.Categories.AnyAsync(x => x.Id == categoryId && x.UserId == categoryOwnerId, ct);
        if (!categoryExists)
        {
            throw new AppException("Category not found.", 404);
        }
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
        if (!debitsAccount)
        {
            return;
        }

        if (account.Type == AccountType.CreditCard)
        {
            var availableCredit = (account.CreditLimit ?? 0) + account.CurrentBalance;
            if (availableCredit < amount)
            {
                throw new AppException($"Limit exceeded for {account.Name}.");
            }

            return;
        }

        if (account.CurrentBalance < amount)
        {
            throw new AppException($"Insufficient funds in {account.Name}.");
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
