namespace Marketplace.Infrastructure.Identity;

public sealed class PasswordResetEmailOptions
{
    public const string SectionName = "PasswordReset";

    public string ResetPageUrl { get; init; } = string.Empty;
}
