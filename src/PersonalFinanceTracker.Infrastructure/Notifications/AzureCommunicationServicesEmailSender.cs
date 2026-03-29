using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using PersonalFinanceTracker.Application.Interfaces;
using PersonalFinanceTracker.Application.Services;

namespace PersonalFinanceTracker.Infrastructure.Notifications;

public class AzureCommunicationServicesEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(AppEmailMessage message, CancellationToken ct = default)
    {
        var connectionString = configuration["Email:AzureCommunicationServices:ConnectionString"];
        var fromAddress = configuration["Email:FromAddress"];

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new AppException("Email provider is not configured.", 500);
        }

        try
        {
            var emailClient = new EmailClient(connectionString);
            var recipients = new EmailRecipients(
                [
                    new EmailAddress(message.ToAddress, message.ToDisplayName)
                ]);
            var content = new EmailContent(message.Subject)
            {
                PlainText = message.PlainTextBody,
                Html = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.PlainTextBody : message.HtmlBody
            };
            var payload = new EmailMessage(fromAddress, recipients, content);

            await emailClient.SendAsync(WaitUntil.Completed, payload, ct);
        }
        catch (RequestFailedException ex)
        {
            throw new AppException($"Unable to send email right now. {ex.Message}", 502);
        }
    }
}
