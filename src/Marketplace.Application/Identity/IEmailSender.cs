namespace Marketplace.Application.Identity;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string Recipient, string Subject, string PlainTextBody, string? HtmlBody = null);

public sealed class EmailDeliveryException(string message, Exception innerException) : Exception(message, innerException);
