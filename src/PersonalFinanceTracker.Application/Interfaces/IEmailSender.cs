namespace PersonalFinanceTracker.Application.Interfaces;

public record AppEmailMessage(
    string ToAddress,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null,
    string? ToDisplayName = null);

public interface IEmailSender
{
    Task SendAsync(AppEmailMessage message, CancellationToken ct = default);
}
