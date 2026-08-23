namespace Marketplace.Infrastructure.Seap;

public sealed class SeapStorageOptions
{
    public const string SectionName = "Seap:Storage";
    public string Bucket { get; init; } = string.Empty;
    public string Region { get; init; } = "eu-central-1";
    public bool Enabled { get; init; }
}
