using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Application.DTOs.Accounts;

public record InviteAccountMemberRequest(
    string Email,
    AccountMemberRole Role);

public record UpdateAccountMemberRequest(AccountMemberRole Role);

public record AccountMemberResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    AccountMemberRole Role,
    bool IsOwner);

public record AccountActivityResponse(
    Guid Id,
    string ActorName,
    string EntityType,
    string Action,
    string Description,
    DateTime CreatedAt);
