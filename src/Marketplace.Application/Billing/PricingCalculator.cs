namespace Marketplace.Application.Billing;

/// <summary>
/// Fair-pricing tiers for document generation (.docx/.xlsx), based on the tender's estimated value.
/// The estimated value from SEAP is in RON; the caller converts it to EUR with a configurable rate
/// before calling <see cref="ComputePriceEur"/>. Pure function: no I/O, no EF — directly unit-testable.
/// Tiers (per generation, EUR):
///   < 20.000 €            → 25 €
///   20.000 – 100.000 €    → 75 €
///   100.000 – 500.000 €   → 300 €
///   500.000 – 1.000.000 € → 500 €
///   1.000.000 – 10.000.000 € → 1.000 €   (the 5M–10M band follows the 1M band)
///   > 10.000.000 €        → 2.000 €
/// The first generation is free (launch offer); the free entitlement is tracked per account by the API.
/// </summary>
public static class PricingCalculator
{
    public const decimal Tier1 = 25;
    public const decimal Tier2 = 75;
    public const decimal Tier3 = 300;
    public const decimal Tier4 = 500;
    public const decimal Tier5 = 1_000;
    public const decimal Tier6 = 2_000;

    public const decimal Tier2Threshold = 20_000m;
    public const decimal Tier3Threshold = 100_000m;
    public const decimal Tier4Threshold = 500_000m;
    public const decimal Tier5Threshold = 1_000_000m;
    public const decimal Tier6Threshold = 10_000_000m;

    public static int ComputePriceEur(decimal estimatedValueEur)
    {
        if (estimatedValueEur < Tier2Threshold) return (int)Tier1;
        if (estimatedValueEur < Tier3Threshold) return (int)Tier2;
        if (estimatedValueEur < Tier4Threshold) return (int)Tier3;
        if (estimatedValueEur < Tier5Threshold) return (int)Tier4;
        if (estimatedValueEur <= Tier6Threshold) return (int)Tier5;
        return (int)Tier6;
    }
}
