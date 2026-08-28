using Marketplace.Domain.Seap;

namespace Marketplace.Domain.Tests;

public sealed class CpvPrefixTests
{
    [Theory]
    [InlineData("45210000", "4521%")]       // buildings class — matches granular 45214200-2 / 45215120-4
    [InlineData("45233000", "4523%")]       // roads class — matches granular 45233100-2
    [InlineData("33600000", "3360%")]       // pharmaceutical class
    [InlineData("15000000", "15%")]         // food division
    [InlineData("45000000", "45%")]         // construction division
    [InlineData("45221111", "4522%")]       // exact bridge code widens to its 4522 class
    [InlineData("45214200", "4521%")]       // granular code widens to its class too
    [InlineData("45", "45%")]
    [InlineData("452", "452%")]
    [InlineData("45233000-1", "4523%")]     // raw value is normalized to the prefix first
    [InlineData(null, "%")]
    [InlineData("", "%")]
    [InlineData("abc", "%")]
    public void ToMatchPatternWidensGroupCodesToTheMeaningfulLevel(string? input, string expected) =>
        Assert.Equal(expected, CpvPrefix.ToMatchPattern(input));
}
