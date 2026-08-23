namespace Marketplace.Application.Billing;

/// <summary>Pricing tiers for auto-bidding, based on the tender's estimated value in RON.
/// Pure function: no I/O, no EF — directly unit-testable.</summary>
public static class PricingCalculator
{
    public const int TierLow = 29;
    public const int TierMedium = 49;
    public const int TierHigh = 99;
    public const decimal MediumThreshold = 100_000m;
    public const decimal HighThreshold = 500_000m;

    public static int ComputePriceEur(decimal estimatedValueRon)
    {
        if (estimatedValueRon < MediumThreshold) return TierLow;
        if (estimatedValueRon <= HighThreshold) return TierMedium;
        return TierHigh;
    }
}
