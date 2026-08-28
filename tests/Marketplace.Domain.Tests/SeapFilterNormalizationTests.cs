using Marketplace.Domain.Seap;

namespace Marketplace.Domain.Tests;

public sealed class SeapFilterNormalizationTests
{
    [Theory]
    [InlineData("IS", "Iași")]
    [InlineData("SV", "Suceava")]
    [InlineData("BT", "Botoșani")]
    [InlineData("CJ", "Cluj")]
    [InlineData("B", "București")]
    [InlineData("is", "Iași")]
    [InlineData("Iasi", "Iași")]
    [InlineData("IAȘI", "Iași")]
    [InlineData("Bistrita-Nasaud", "Bistrița-Năsăud")]
    [InlineData("Bistrița-Năsăud", "Bistrița-Năsăud")]
    public void NormalizesCountyCodesAndNamesToCanonicalNames(string value, string expected)
    {
        Assert.Equal(expected, RomanianCountyCodes.TryNormalize(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("XX")]
    [InlineData("Muntenia")]
    [InlineData("Dobrogea")]
    public void ReturnsNullForUnknownCounties(string? value)
    {
        Assert.Null(RomanianCountyCodes.TryNormalize(value));
    }

    [Theory]
    [InlineData("45233000", "45233000")]
    [InlineData("45233000-1", "45233000")]
    [InlineData("45233000 - Lucrări de drumuri", "45233000")]
    [InlineData("45", "45")]
    [InlineData("4522111100", "45221111")]
    public void NormalizesCpvCodesAndPrefixesToDigitPrefix(string value, string expected)
    {
        Assert.Equal(expected, CpvPrefix.TryNormalize(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("Lucrări de drumuri")]
    public void ReturnsNullWhenCpvValueHasNoDigits(string? value)
    {
        Assert.Null(CpvPrefix.TryNormalize(value));
    }

    [Fact]
    public void AllCountiesContainsEveryCanonicalCountyNameOnce()
    {
        Assert.Equal(42, RomanianCountyCodes.AllCounties.Count);
        Assert.Equal(42, RomanianCountyCodes.AllCounties.Distinct().Count());
        Assert.Contains("Iași", RomanianCountyCodes.AllCounties);
        Assert.Contains("Suceava", RomanianCountyCodes.AllCounties);
        Assert.Contains("Botoșani", RomanianCountyCodes.AllCounties);
    }
}
