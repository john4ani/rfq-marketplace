namespace Marketplace.Infrastructure;

/// <summary>
/// Master switches for the platform. All flags are false at deploy time and can be toggled live through
/// configuration (IOptionsMonitor reads appsettings.json on change, no redeploy).
/// MarketPlaceEnabled gates every marketplace surface (buyer/supplier journeys, live market, RFQs, offers,
/// profiles) except the standalone Anunțuri SEAP product and the Administrator workspace: when it is false
/// the UI hides marketplace navigation and the API redirects marketplace requests, leaving only SEAP + Admin.
/// </summary>
public sealed class FeaturesOptions
{
    public const string SectionName = "Features";
    public bool SeapIngestionEnabled { get; init; }
    public bool AutoBiddingEnabled { get; init; }
    public bool MarketPlaceEnabled { get; init; }
}
