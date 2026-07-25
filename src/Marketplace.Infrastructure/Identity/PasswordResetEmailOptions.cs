namespace Marketplace.Infrastructure.Identity;

public sealed class PasswordResetEmailOptions
{
    public const string SectionName = "PasswordReset";

    public string ResetPageUrl { get; init; } = string.Empty;
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "RFQ Marketplace";
    public bool EnableSsl { get; init; } = true;
}
