namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed class SupplierOutreachOptions
{
    public const string SectionName = "SupplierOutreach";
    public int EmailCooldownHours { get; init; } = 72;
    public string PublicApplicationUrl { get; init; } = "https://www.compaspot.com";
}
