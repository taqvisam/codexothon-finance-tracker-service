using PersonalFinanceTracker.Domain.Entities;
using PersonalFinanceTracker.Infrastructure.Data;

namespace PersonalFinanceTracker.Infrastructure.Repositories;

public class AccountActivityLogger(AppDbContext dbContext)
{
    public void Log(Guid accountId, Guid actorUserId, string entityType, string action, string description, Guid? entityId = null)
    {
        dbContext.AccountActivities.Add(new AccountActivity
        {
            AccountId = accountId,
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Description = description
        });
    }
}
