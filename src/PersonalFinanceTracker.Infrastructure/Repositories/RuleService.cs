using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Rules;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;
using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class RuleService(AppDbContext dbContext) : IRuleService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RuleResponse>> GetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var rules = await dbContext.Rules
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return rules.Select(Map).ToList();
    }

    public async Task<RuleResponse> CreateAsync(Guid userId, RuleRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var entity = new Rule
        {
            UserId = userId,
            Name = request.Name.Trim(),
            ConditionJson = JsonSerializer.Serialize(request.Condition, JsonOptions),
            ActionJson = JsonSerializer.Serialize(request.Action, JsonOptions),
            Priority = request.Priority,
            IsActive = request.IsActive
        };

        dbContext.Rules.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<RuleResponse> UpdateAsync(Guid userId, Guid id, RuleRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var entity = await dbContext.Rules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Rule not found.", 404);

        entity.Name = request.Name.Trim();
        entity.ConditionJson = JsonSerializer.Serialize(request.Condition, JsonOptions);
        entity.ActionJson = JsonSerializer.Serialize(request.Action, JsonOptions);
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var entity = await dbContext.Rules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new AppException("Rule not found.", 404);
        dbContext.Rules.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
    }

    private static RuleResponse Map(Rule entity)
        => new(
            entity.Id,
            entity.Name,
            JsonSerializer.Deserialize<RuleConditionDto>(entity.ConditionJson, JsonOptions)!,
            JsonSerializer.Deserialize<RuleActionDto>(entity.ActionJson, JsonOptions)!,
            entity.Priority,
            entity.IsActive);

    private static void Validate(RuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("Rule name is required.", 400);
        }

        if (request.Priority < 0)
        {
            throw new AppException("Priority must be zero or greater.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Condition.Value))
        {
            throw new AppException("Rule condition value is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Action.Value))
        {
            throw new AppException("Rule action value is required.", 400);
        }
    }
}
