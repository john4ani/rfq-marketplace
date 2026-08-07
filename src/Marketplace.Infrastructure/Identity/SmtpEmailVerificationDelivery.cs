using System.Net;
using System.Net.Mail;
using Marketplace.Application.Identity;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Identity;

public sealed class SmtpEmailVerificationDelivery(IOptions<EmailVerificationOptions> options) : IEmailVerificationDelivery
{
    private readonly EmailVerificationOptions options = options.Value;
    public async Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{options.ConfirmationPageUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            using var message = new MailMessage { From = new MailAddress(options.FromAddress, options.FromName), Subject = "Confirmă adresa de e-mail CompaSpot", Body = $"Confirmă adresa de e-mail deschizând acest link:\n{url}\n\nDacă nu ai cerut confirmarea, poți ignora mesajul.", IsBodyHtml = false };
            message.To.Add(new MailAddress(email));
            using var client = new SmtpClient(options.SmtpHost, options.SmtpPort) { EnableSsl = options.EnableSsl, Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword) };
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException) { throw new EmailVerificationDeliveryException("Email-verification delivery failed.", exception); }
    }
}
