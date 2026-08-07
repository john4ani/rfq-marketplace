namespace Marketplace.Application.Rfqs;

public enum ProviderCoverageMatch { None, Full, Partial }

public static class ProviderCoveragePolicy
{
    public static ProviderCoverageMatch Decide(
        bool allowSplit,
        IReadOnlySet<Guid> requiredCategories,
        IReadOnlySet<Guid> providerCategories)
    {
        if (requiredCategories.IsSubsetOf(providerCategories)) return ProviderCoverageMatch.Full;
        if (!allowSplit) return ProviderCoverageMatch.None;
        return requiredCategories.Overlaps(providerCategories)
            ? ProviderCoverageMatch.Partial
            : ProviderCoverageMatch.None;
    }
}
