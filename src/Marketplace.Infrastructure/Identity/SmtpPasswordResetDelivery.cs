using System.Net;
using System.Net.Mail;
using Marketplace.Application.Identity;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Identity;

public sealed class SmtpPasswordResetDelivery(IOptions<PasswordResetEmailOptions> options) : IPasswordResetDelivery
{
    private readonly PasswordResetEmailOptions _options = options.Value;

    public async Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        try
        {
            string resetUrl = $"{_options.ResetPageUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = "Resetarea parolei RFQ Marketplace",
                Body = $"""
                    A fost solicitată resetarea parolei contului tău RFQ Marketplace.

                    Deschide linkul de mai jos pentru a alege o parolă nouă:
                    {resetUrl}

                    Dacă nu ai făcut această solicitare, poți ignora mesajul.
                    """,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(email));

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.EnableSsl,
                Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword)
            };
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            throw new PasswordResetDeliveryException("Password-reset email delivery failed.", exception);
        }
    }
}
