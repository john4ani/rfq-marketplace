namespace Marketplace.Infrastructure.Identity;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";
    public bool EnableSsl { get; init; } = true;
    public string ConfirmationPageUrl { get; init; } = "http://localhost:4200/verify-email";
}
