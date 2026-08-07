namespace Marketplace.Infrastructure.Identity;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "CompaSpot";
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public bool EnableSsl { get; init; } = true;
    public string ConfirmationPageUrl { get; init; } = "http://localhost:4200/verify-email";
}
