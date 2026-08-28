namespace Marketplace.Application.Billing;

/// <summary>
/// Fair-pricing tiers for document generation (.docx/.xlsx), based on the tender's estimated value.
/// The estimated value from SEAP is in RON; the caller converts it to EUR with a configurable rate
/// before calling <see cref="ComputePriceEur"/>. Pure function: no I/O, no EF — directly unit-testable.
/// Tiers (per generation, EUR; each band is [lower, upper)):
///   < 20.000 €                  → 25 €
///   20.000 – 100.000 €          → 75 €
///   100.000 – 500.000 €         → 300 €
///   500.000 – 1.000.000 €       → 500 €
///   1.000.000 – 5.000.000 €     → 1.000 €
///   5.000.000 – 10.000.000 €    → 2.000 €
///   10.000.000 – 15.000.000 €   → 3.000 €
///   15.000.000 – 20.000.000 €   → 4.000 €
///   20.000.000 – 25.000.000 €   → 5.000 €
///   25.000.000 – 30.000.000 €   → 6.000 €
///   30.000.000 – 35.000.000 €   → 7.000 €
///   35.000.000 – 40.000.000 €   → 8.000 €
///   40.000.000 – 45.000.000 €   → 9.000 €
///   45.000.000 – 50.000.000 €   → 10.000 €
///   ≥ 50.000.000 €              → 20.000 €
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
    public const decimal Tier7 = 3_000;
    public const decimal Tier8 = 4_000;
    public const decimal Tier9 = 5_000;
    public const decimal Tier10 = 6_000;
    public const decimal Tier11 = 7_000;
    public const decimal Tier12 = 8_000;
    public const decimal Tier13 = 9_000;
    public const decimal Tier14 = 10_000;
    public const decimal Tier15 = 20_000;

    public const decimal Tier2Threshold = 20_000m;
    public const decimal Tier3Threshold = 100_000m;
    public const decimal Tier4Threshold = 500_000m;
    public const decimal Tier5Threshold = 1_000_000m;
    public const decimal Tier6Threshold = 5_000_000m;
    public const decimal Tier7Threshold = 10_000_000m;
    public const decimal Tier8Threshold = 15_000_000m;
    public const decimal Tier9Threshold = 20_000_000m;
    public const decimal Tier10Threshold = 25_000_000m;
    public const decimal Tier11Threshold = 30_000_000m;
    public const decimal Tier12Threshold = 35_000_000m;
    public const decimal Tier13Threshold = 40_000_000m;
    public const decimal Tier14Threshold = 45_000_000m;
    public const decimal Tier15Threshold = 50_000_000m;

    public static int ComputePriceEur(decimal estimatedValueEur)
    {
        if (estimatedValueEur < Tier2Threshold) return (int)Tier1;
        if (estimatedValueEur < Tier3Threshold) return (int)Tier2;
        if (estimatedValueEur < Tier4Threshold) return (int)Tier3;
        if (estimatedValueEur < Tier5Threshold) return (int)Tier4;
        if (estimatedValueEur < Tier6Threshold) return (int)Tier5;
        if (estimatedValueEur < Tier7Threshold) return (int)Tier6;
        if (estimatedValueEur < Tier8Threshold) return (int)Tier7;
        if (estimatedValueEur < Tier9Threshold) return (int)Tier8;
        if (estimatedValueEur < Tier10Threshold) return (int)Tier9;
        if (estimatedValueEur < Tier11Threshold) return (int)Tier10;
        if (estimatedValueEur < Tier12Threshold) return (int)Tier11;
        if (estimatedValueEur < Tier13Threshold) return (int)Tier12;
        if (estimatedValueEur < Tier14Threshold) return (int)Tier13;
        if (estimatedValueEur < Tier15Threshold) return (int)Tier14;
        return (int)Tier15;
    }
}
