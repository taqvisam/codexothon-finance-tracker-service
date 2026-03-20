using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Recurring;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class RecurringService(AppDbContext dbContext) : IRecurringService
{
    public async Task<IReadOnlyList<RecurringResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.RecurringTransactions
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.NextRunDate)
            .Select(x => new RecurringResponse(x.Id, x.Title, x.Type, x.Amount, x.CategoryId, x.AccountId, x.Frequency, x.NextRunDate, x.AutoCreateTransaction, x.IsPaused))
            .ToListAsync(ct);
    }

    public async Task<RecurringResponse> CreateAsync(Guid userId, RecurringRequest request, CancellationToken ct = default)
    {
        var entity = new RecurringTransaction
        {
            UserId = userId,
            Title = request.Title,
            Type = request.Type,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextRunDate = request.NextRunDate,
            AutoCreateTransaction = request.AutoCreateTransaction,
            IsPaused = request.IsPaused
        };

        dbContext.RecurringTransactions.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return new RecurringResponse(entity.Id, entity.Title, entity.Type, entity.Amount, entity.CategoryId, entity.AccountId, entity.Frequency, entity.NextRunDate, entity.AutoCreateTransaction, entity.IsPaused);
    }

    public async Task<RecurringResponse> UpdateAsync(Guid userId, Guid id, RecurringRequest request, CancellationToken ct = default)
    {
        var entity = await dbContext.RecurringTransactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Recurring item not found.", 404);

        entity.Title = request.Title;
        entity.Type = request.Type;
        entity.Amount = request.Amount;
        entity.CategoryId = request.CategoryId;
        entity.AccountId = request.AccountId;
        entity.Frequency = request.Frequency;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.NextRunDate = request.NextRunDate;
        entity.AutoCreateTransaction = request.AutoCreateTransaction;
        entity.IsPaused = request.IsPaused;

        await dbContext.SaveChangesAsync(ct);
        return new RecurringResponse(entity.Id, entity.Title, entity.Type, entity.Amount, entity.CategoryId, entity.AccountId, entity.Frequency, entity.NextRunDate, entity.AutoCreateTransaction, entity.IsPaused);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var entity = await dbContext.RecurringTransactions.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Recurring item not found.", 404);
        dbContext.RecurringTransactions.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
    }
}
