namespace Marketplace.Infrastructure.Identity;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseTls { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "CompaSpot";
    public string ReplyToAddress { get; init; } = string.Empty;
}
