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
    [InlineData("Cluj-Napoca", "Cluj")]
    [InlineData("CLUJ NAPOCA", "Cluj")]
    [InlineData("Targu Mures", "Mureș")]
    [InlineData("Târgu-Mureș", "Mureș")]
    [InlineData("Alba Iulia", "Alba")]
    [InlineData("Baia Mare", "Maramureș")]
    [InlineData("Oradea", "Bihor")]
    [InlineData("Craiova", "Dolj")]
    [InlineData("Ploiesti", "Prahova")]
    [InlineData("Drobeta-Turnu Severin", "Mehedinți")]
    [InlineData("Piatra Neamt", "Neamț")]
    [InlineData("Sfantu Gheorghe", "Covasna")]
    public void NormalizesResidenceCityAliasesToTheirCounty(string value, string expected)
    {
        // The import parser (CountyParser) and the SEAP Tender Match normalizer share one vocabulary,
        // so a city like Cluj-Napoca must normalize to the same canonical county the tenders store.
        Assert.Equal(expected, RomanianCountyCodes.TryNormalize(value));
    }

    [Fact]
    public void CountyParserAndNormalizerShareTheSameAliasVocabulary()
    {
        // Every alias must map to a county that CountyParser can also derive from an authority name,
        // i.e. the shared table is the single source of truth and stays consistent.
        Assert.Equal(42, RomanianCountyCodes.CountyAliases.Length);
        foreach ((string county, string[] aliases) in RomanianCountyCodes.CountyAliases)
        {
            Assert.Contains(county, RomanianCountyCodes.AllCounties);
            Assert.All(aliases, alias => Assert.Equal(county, RomanianCountyCodes.TryNormalize(alias)));
        }
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
