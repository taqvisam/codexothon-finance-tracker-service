using PersonalFinanceTracker.Domain.Enums;

namespace PersonalFinanceTracker.Domain.Entities;

public class AccountMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public AccountMemberRole Role { get; set; } = AccountMemberRole.Viewer;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Account? Account { get; set; }
    public User? User { get; set; }
}
