using Marketplace.Application.Rfqs;

namespace Marketplace.Application.Tests;

public sealed class ProviderCoveragePolicyTests
{
    private static readonly Guid CategoryA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CategoryB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void FullCoverageProviderReceivesWholeRequest()
    {
        ProviderCoverageMatch result = ProviderCoveragePolicy.Decide(
            allowSplit: false,
            new HashSet<Guid> { CategoryA, CategoryB },
            new HashSet<Guid> { CategoryA, CategoryB },
            canFulfillMultiCategoryRequests: true,
            fullCoverageProviderExists: true);

        Assert.Equal(ProviderCoverageMatch.Full, result);
    }

    [Fact]
    public void SpecialistDoesNotReceiveRequestWhenBuyerDisallowsSplit()
    {
        ProviderCoverageMatch result = ProviderCoveragePolicy.Decide(
            allowSplit: false,
            new HashSet<Guid> { CategoryA, CategoryB },
            new HashSet<Guid> { CategoryA },
            canFulfillMultiCategoryRequests: false,
            fullCoverageProviderExists: false);

        Assert.Equal(ProviderCoverageMatch.None, result);
    }

    [Fact]
    public void SpecialistReceivesPartialRequestOnlyWhenSplitAllowedAndNoFullProviderExists()
    {
        HashSet<Guid> required = [CategoryA, CategoryB];
        HashSet<Guid> specialist = [CategoryA];

        Assert.Equal(ProviderCoverageMatch.None,
            ProviderCoveragePolicy.Decide(true, required, specialist, false, fullCoverageProviderExists: true));
        Assert.Equal(ProviderCoverageMatch.Partial,
            ProviderCoveragePolicy.Decide(true, required, specialist, false, fullCoverageProviderExists: false));
    }

    [Fact]
    public void SelectingSeveralCategoriesDoesNotImplyWholeRequestCapability()
    {
        HashSet<Guid> required = [CategoryA, CategoryB];

        Assert.Equal(
            ProviderCoverageMatch.None,
            ProviderCoveragePolicy.Decide(false, required, required, false, fullCoverageProviderExists: false));
        Assert.Equal(
            ProviderCoverageMatch.Full,
            ProviderCoveragePolicy.Decide(false, required, required, true, fullCoverageProviderExists: true));
    }
}
