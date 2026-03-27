using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Application.DTOs.Rules;
using PersonalFinanceTracker.Application.DTOs.Transactions;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Domain.Enums;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class RuleEngineService(AppDbContext dbContext) : IRuleEngineService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TransactionResponse> ApplyAsync(Guid userId, Guid accountOwnerId, TransactionRequest request, CancellationToken ct = default)
    {
        var rules = await dbContext.Rules
            .Where(x => x.UserId == accountOwnerId && x.IsActive)
            .OrderBy(x => x.Priority)
            .ToListAsync(ct);

        var effectiveCategoryId = request.CategoryId;
        var tags = request.Tags is { Count: > 0 } ? new List<string>(request.Tags) : new List<string>();
        var alerts = new List<string>();

        string? categoryName = null;
        if (request.CategoryId.HasValue)
        {
            categoryName = await dbContext.Categories
                .Where(x => x.Id == request.CategoryId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct);
        }

        foreach (var rule in rules)
        {
            var condition = JsonSerializer.Deserialize<RuleConditionDto>(rule.ConditionJson, JsonOptions);
            var action = JsonSerializer.Deserialize<RuleActionDto>(rule.ActionJson, JsonOptions);
            if (condition is null || action is null || !Matches(condition, request, categoryName))
            {
                continue;
            }

            switch (action.Type)
            {
                case RuleActionType.SetCategory:
                {
                    var matchedCategoryId = await dbContext.Categories
                        .Where(x => x.UserId == accountOwnerId && x.Name.ToLower() == action.Value.Trim().ToLower())
                        .Select(x => (Guid?)x.Id)
                        .FirstOrDefaultAsync(ct);
                    if (matchedCategoryId.HasValue)
                    {
                        effectiveCategoryId = matchedCategoryId.Value;
                        categoryName = action.Value.Trim();
                    }
                    break;
                }
                case RuleActionType.AddTag:
                    if (!tags.Contains(action.Value.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        tags.Add(action.Value.Trim());
                    }
                    break;
                case RuleActionType.TriggerAlert:
                    alerts.Add(action.Value.Trim());
                    break;
            }
        }

        return new TransactionResponse(
            Guid.Empty,
            request.AccountId,
            effectiveCategoryId,
            request.Type,
            request.Amount,
            request.Date,
            request.Merchant,
            request.Note,
            request.PaymentMethod,
            request.TransferAccountId,
            tags,
            alerts);
    }

    private static bool Matches(RuleConditionDto condition, TransactionRequest request, string? categoryName)
    {
        return condition.Field switch
        {
            RuleField.Merchant => MatchText(request.Merchant, condition),
            RuleField.Note => MatchText(request.Note, condition),
            RuleField.PaymentMethod => MatchText(request.PaymentMethod, condition),
            RuleField.Category => MatchText(categoryName, condition),
            RuleField.Type => MatchText(request.Type.ToString(), condition),
            RuleField.Amount => MatchNumber(request.Amount, condition),
            _ => false
        };
    }

    private static bool MatchText(string? actual, RuleConditionDto condition)
    {
        actual ??= string.Empty;
        var expected = condition.Value.Trim();
        return condition.Operator switch
        {
            RuleOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.StartsWith => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            RuleOperator.EndsWith => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool MatchNumber(decimal actual, RuleConditionDto condition)
    {
        if (!decimal.TryParse(condition.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        return condition.Operator switch
        {
            RuleOperator.Equals => actual == expected,
            RuleOperator.GreaterThan => actual > expected,
            RuleOperator.LessThan => actual < expected,
            _ => false
        };
    }
}
