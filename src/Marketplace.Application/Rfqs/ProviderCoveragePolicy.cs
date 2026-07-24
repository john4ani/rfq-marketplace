namespace Marketplace.Application.Rfqs;

public enum ProviderCoverageMatch { None, Full, Partial }

public static class ProviderCoveragePolicy
{
    public static ProviderCoverageMatch Decide(
        bool allowSplit,
        IReadOnlySet<Guid> requiredCategories,
        IReadOnlySet<Guid> providerCategories,
        bool canFulfillMultiCategoryRequests,
        bool fullCoverageProviderExists)
    {
        bool canCoverWholeRequest = requiredCategories.IsSubsetOf(providerCategories)
            && (requiredCategories.Count == 1 || canFulfillMultiCategoryRequests);
        if (canCoverWholeRequest) return ProviderCoverageMatch.Full;
        if (!allowSplit || fullCoverageProviderExists) return ProviderCoverageMatch.None;
        return requiredCategories.Overlaps(providerCategories)
            ? ProviderCoverageMatch.Partial
            : ProviderCoverageMatch.None;
    }
}
