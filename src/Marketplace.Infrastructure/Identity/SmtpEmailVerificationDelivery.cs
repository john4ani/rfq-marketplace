using Marketplace.Application.Identity;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Identity;

public sealed class SmtpEmailVerificationDelivery(
    IOptions<EmailVerificationOptions> options,
    IEmailSender emailSender) : IEmailVerificationDelivery
{
    private readonly EmailVerificationOptions options = options.Value;
    public async Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"{options.ConfirmationPageUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendAsync(new(email, "Confirmă adresa de e-mail CompaSpot",
                $"Confirmă adresa de e-mail deschizând acest link:\n{url}\n\nDacă nu ai cerut confirmarea, poți ignora mesajul."), cancellationToken);
        }
        catch (EmailDeliveryException exception) { throw new EmailVerificationDeliveryException("Email-verification delivery failed.", exception); }
    }
}
