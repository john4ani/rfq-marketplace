using Marketplace.Application.Identity;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Identity;

public sealed class SmtpPasswordResetDelivery(
    IOptions<PasswordResetEmailOptions> options,
    IEmailSender emailSender) : IPasswordResetDelivery
{
    private readonly PasswordResetEmailOptions _options = options.Value;

    public async Task SendAsync(string email, string token, CancellationToken cancellationToken)
    {
        try
        {
            string resetUrl = $"{_options.ResetPageUrl}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendAsync(new(
                email,
                "Resetarea parolei CompaSpot",
                $"""
                    A fost solicitată resetarea parolei contului tău RFQ Marketplace.

                    Deschide linkul de mai jos pentru a alege o parolă nouă:
                    {resetUrl}

                    Dacă nu ai făcut această solicitare, poți ignora mesajul.
                    """), cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            throw new PasswordResetDeliveryException("Password-reset email delivery failed.", exception);
        }
    }
}
