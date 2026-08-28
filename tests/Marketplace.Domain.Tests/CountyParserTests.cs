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
    [InlineData("REGIA AUTONOMA JUDETEANA DRUMURI SI PODURI VALCEA", "Vâlcea")]
    [InlineData("CONSILIUL JUDETEAN MURES", "Mureș")]
    [InlineData("CONSILIUL JUDETEAN BIHOR", "Bihor")]
    [InlineData("CONSILIUL JUDETEAN VRANCEA", "Vrancea")]
    [InlineData("DIRECTIA JUDETEANA DE TRANSPORT NEAMT", "Neamț")]
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
    [InlineData("PRIMARIA COMUNEI MARGINEA")] // ambiguous (Bacău/Suceava) with no county hint → stays unknown
    [InlineData("SC BRADUL SA")]               // company name, no local-administration marker
    public void ReturnsNullWhenNoCountyIsRecognizable(string? authorityName)
    {
        Assert.Null(CountyParser.Parse(authorityName));
    }

    [Theory]
    [InlineData("PRIMARIA COMUNEI ARBORE", "Suceava")]            // unique commune under a marker
    [InlineData("COMUNA CHIAJNA", "Ilfov")]                       // non-seat commune in Ilfov
    [InlineData("PRIMARIA ORASULUI DOROHOI", "Botoșani")]         // non-seat town
    [InlineData("PRIMARIA COMUNEI MARGINEA, JUD. SV", "Suceava")] // ambiguous commune + explicit county code
    [InlineData("PRIMARIA COMUNEI MARGINEA, JUD SV", "Suceava")]
    [InlineData("PRIMARIA COMUNEI MARGINEA, JUDETUL SV", "Suceava")]
    [InlineData("PRIMARIA COMUNEI MARGINEA, JUDETUL SUCEAVA", "Suceava")] // spelled-out county wins
    [InlineData("PRIMARIA COMUNEI MARGINEA PADURII", "Prahova")]         // 2-word unique locality
    public void RecognizesCommuneLocalityNamesAndCountyCodes(string authorityName, string expectedCounty)
    {
        Assert.Equal(expectedCounty, CountyParser.Parse(authorityName));
    }

    [Fact]
    public void LocalityVocabularyCoversCommunesAcrossCounties()
    {
        // The embedded resource (SIRUTA-derived, unambiguous names only) covers all counties, so a
        // commune tender matches its county even without the county spelled out.
        Assert.Equal("Suceava", CountyParser.Parse("PRIMARIA COMUNEI ARBORE"));
        Assert.Equal("Ilfov", CountyParser.Parse("COMUNA CHIAJNA"));
        Assert.Equal("Botoșani", CountyParser.Parse("PRIMARIA ORASULUI DOROHOI"));
    }

    [Fact]
    public void DiacriticsDoNotBreakCountyDetection()
    {
        // "Muntenia" is not a county, but the point is that "Argeș" must match despite diacritics.
        Assert.Equal("Argeș", CountyParser.Parse("PRIMARIA MUNICIPIULUI ARGES"));
        Assert.Equal("Iași", CountyParser.Parse("PRIMARIA MUNICIPIULUI IAȘI"));
    }
}