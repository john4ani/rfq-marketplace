using Marketplace.Domain.Seap;

namespace Marketplace.Domain.Tests;

public sealed class CountyParserTests
{
    [Theory]
    [InlineData("17095927 - DIRECTIA GENERALA DE ASISTENTA SOCIALA SI PROTECTIA COPILULUI VASLUI", "Vaslui")]
    [InlineData("MUNICIPIUL CLUJ-NAPOCA", "Cluj")]
    [InlineData("PRIMARIA MUNICIPIULUI CLUJ-NAPOCA", "Cluj")]
    [InlineData("COMUNA GIURGIU", "Giurgiu")]
    [InlineData("PRIMARIA MUNICIPIULUI IASI", "Iași")]
    [InlineData("PRIMARIA MUNICIPIULUI IASI, judetul Iasi", "Iași")]
    [InlineData("PRIMARIA MUNICIPIULUI ORADEA", "Bihor")]
    [InlineData("PRIMARIA MUNICIPIULUI PITESTI", "Argeș")]
    [InlineData("CONSILIUL JUDETEAN TIMIS", "Timiș")]
    [InlineData("SC ECOVOL ILFOV SA", "Ilfov")]
    [InlineData("4374873 - SPITALUL JUDETEAN DE URGENTA SFANTUL GHEORGHE", "Covasna")]
    public void RecognizesCountyAndResidenceCityAliases(string authorityName, string expectedCounty)
    {
        Assert.Equal(expectedCounty, CountyParser.Parse(authorityName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("4374873 - SPITALUL DE URGENTA PETROSANI")]
    [InlineData("SERVICIUL PUBLIC DE ADMINISTRARE A PATRIMONIULUI")]
    public void ReturnsNullWhenNoCountyIsRecognizable(string? authorityName)
    {
        Assert.Null(CountyParser.Parse(authorityName));
    }

    [Fact]
    public void DiacriticsDoNotBreakCountyDetection()
    {
        // "Muntenia" is not a county, but the point is that "Argeș" must match despite diacritics.
        Assert.Equal("Argeș", CountyParser.Parse("PRIMARIA MUNICIPIULUI ARGES"));
        Assert.Equal("Iași", CountyParser.Parse("PRIMARIA MUNICIPIULUI IAȘI"));
    }
}