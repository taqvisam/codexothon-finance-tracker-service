using Microsoft.Extensions.Logging;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Infrastructure.Notifications;

public class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(AppEmailMessage message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "Email sending skipped because no provider is configured. Intended recipient: {Recipient}, subject: {Subject}",
            message.ToAddress,
            message.Subject);
        return Task.CompletedTask;
    }
}
