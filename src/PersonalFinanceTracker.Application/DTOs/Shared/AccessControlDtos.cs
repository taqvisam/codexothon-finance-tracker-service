using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Shared;

public record AccountAccessContext(
    Guid AccountId,
    Guid OwnerUserId,
    AccountMemberRole Role,
    bool IsOwner)
{
    public bool CanEdit => IsOwner || Role == AccountMemberRole.Editor;
}
