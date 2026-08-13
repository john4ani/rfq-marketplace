using System.Net;
using System.Net.Mail;
using Marketplace.Application.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Identity;

public sealed partial class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions options = options.Value;

    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Error,
        Message = "SMTP delivery failed. Host: {Host}; port: {Port}; TLS: {UseTls}; authentication: {AuthenticationMode}; recipient domain: {RecipientDomain}. For an IP-authenticated relay, authorize the API host public IP. For an authenticated relay, configure both Email__Username and Email__Password.")]
    private static partial void LogDeliveryFailure(
        ILogger logger,
        Exception exception,
        string host,
        int port,
        bool useTls,
        string authenticationMode,
        string recipientDomain);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!options.Enabled) return;
        try
        {
            using MailMessage mailMessage = CreateMailMessage(options, message);
            using var client = new SmtpClient(options.Host, options.Port) { EnableSsl = options.UseTls };
            if (!string.IsNullOrWhiteSpace(options.Username) || !string.IsNullOrWhiteSpace(options.Password))
                client.Credentials = new NetworkCredential(options.Username, options.Password);
            await client.SendMailAsync(mailMessage, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            LogDeliveryFailure(
                logger,
                exception,
                options.Host,
                options.Port,
                options.UseTls,
                HasCredentials(options) ? "username/password" : "IP allow-list",
                DomainOf(message.Recipient));
            throw new EmailDeliveryException("Email delivery failed.", exception);
        }
    }

    private static string DomainOf(string address) => address.Split('@').LastOrDefault() ?? "invalid";

    private static bool HasCredentials(EmailOptions emailOptions) =>
        !string.IsNullOrWhiteSpace(emailOptions.Username) || !string.IsNullOrWhiteSpace(emailOptions.Password);

    internal static MailMessage CreateMailMessage(EmailOptions options, EmailMessage message)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(options.FromAddress, options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody ?? message.PlainTextBody,
            IsBodyHtml = message.HtmlBody is not null
        };
        mailMessage.To.Add(new MailAddress(message.Recipient));
        mailMessage.ReplyToList.Add(new MailAddress(options.ReplyToAddress));
        return mailMessage;
    }
}
