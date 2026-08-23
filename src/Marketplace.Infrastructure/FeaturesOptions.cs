namespace Marketplace.Infrastructure;

/// <summary>
/// Master safety switches for the V3 module (Anunțuri SEAP). Both are false at deploy time and can be
/// toggled live through configuration (IOptionsMonitor reads appsettings.json on change, no redeploy).
/// The base MVP application never depends on these flags.
/// </summary>
public sealed class FeaturesOptions
{
    public const string SectionName = "Features";
    public bool SeapIngestionEnabled { get; init; }
    public bool AutoBiddingEnabled { get; init; }
}
