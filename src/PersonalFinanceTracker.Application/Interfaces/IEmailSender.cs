namespace PersonalFinanceTracker.Application.Interfaces;

public record AppEmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content,
    string? ContentId = null);

public record AppEmailMessage(
    string ToAddress,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null,
    string? ToDisplayName = null,
    IReadOnlyList<AppEmailAttachment>? Attachments = null);

public interface IEmailSender
{
    Task SendAsync(AppEmailMessage message, CancellationToken ct = default);
}
