using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Marketplace.Domain.Seap;

/// <summary>
/// Deterministic county extraction from a SEAP contracting-authority name (e.g.
/// "17095927 - DIRECTIA GENERALA DE ASISTENTA SOCIALA SI PROTECTIA COPILULUI VASLUI" → "Vaslui",
/// "MUNICIPIUL CLUJ-NAPOCA" → "Cluj", "COMUNA GIURGIU" → "Giurgiu"). No AI is needed: authority names
/// routinely end with the county seat or the county itself, so a token match against the 42 Romanian
/// counties (county name + residence city aliases, diacritics-insensitive) is reliable. Returns null
/// when the name carries no recognizable county — the tender is then excluded only when an explicit
/// radius filter is active.
/// </summary>
public static partial class CountyParser
{
    /// <summary>County name with its aliases (county name + residence city, all WITHOUT diacritics —
    /// matching runs on the diacritics-stripped input).</summary>
    private static readonly (string County, string[] Aliases)[] CountyAliases =
    [
        ("Alba", ["ALBA IULIA", "ALBA"]),
        ("Arad", ["ARAD"]),
        ("Argeș", ["PITESTI", "ARGES"]),
        ("Bacău", ["BACAU"]),
        ("Bihor", ["ORADEA"]),
        ("Bistrița-Năsăud", ["BISTRITA NASAUD", "BISTRITA"]),
        ("Botoșani", ["BOTOSANI"]),
        ("Brașov", ["BRASOV"]),
        ("Brăila", ["BRAILA"]),
        ("București", ["BUCURESTI", "BUCHAREST"]),
        ("Buzău", ["BUZAU"]),
        ("Caraș-Severin", ["RESITA", "CARAS SEVERIN"]),
        ("Călărași", ["CALARASI"]),
        ("Cluj", ["CLUJ NAPOCA", "CLUJ"]),
        ("Constanța", ["CONSTANTA"]),
        ("Covasna", ["SFANTU GHEORGHE", "SFANTUL GHEORGHE"]),
        ("Dâmbovița", ["TARGOVISTE"]),
        ("Dolj", ["CRAIOVA"]),
        ("Galați", ["GALATI"]),
        ("Giurgiu", ["GIURGIU"]),
        ("Gorj", ["TARGU JIU"]),
        ("Harghita", ["MIERCUREA CIUC"]),
        ("Hunedoara", ["DEVA"]),
        ("Ialomița", ["SLOBOZIA"]),
        ("Iași", ["IASI"]),
        ("Ilfov", ["ILFOV", "BUFTEA"]),
        ("Maramureș", ["BAIA MARE"]),
        ("Mehedinți", ["DROBETA TURNU SEVERIN"]),
        ("Mureș", ["TARGU MURES"]),
        ("Neamț", ["PIATRA NEAMT"]),
        ("Olt", ["SLATINA"]),
        ("Prahova", ["PLOIESTI"]),
        ("Satu Mare", ["SATU MARE"]),
        ("Sălaj", ["ZALAU"]),
        ("Sibiu", ["SIBIU"]),
        ("Suceava", ["SUCEAVA"]),
        ("Teleorman", ["ALEXANDRIA"]),
        ("Timiș", ["TIMISOARA", "TIMIS"]),
        ("Tulcea", ["TULCEA"]),
        ("Vaslui", ["VASLUI"]),
        ("Vâlcea", ["RAMNICU VALCEA"]),
        ("Vrancea", ["FOCSANI"])
    ];

    /// <summary>Parses the county of a SEAP contracting-authority name, or null when none is recognizable.</summary>
    public static string? Parse(string? authorityName)
    {
        if (string.IsNullOrWhiteSpace(authorityName)) return null;

        string normalized = Normalize(authorityName);
        if (normalized.Length == 0) return null;

        string? bestCounty = null;
        int bestIndex = -1;
        int bestAliasLength = 0;
        foreach ((string county, string[] aliases) in CountyAliases)
        {
            foreach (string alias in aliases)
            {
                // Authority names end with the locality, so the LAST matching alias wins; on a tie the
                // longest alias (e.g. "CLUJ NAPOCA" over "CLUJ") is the most specific.
                Match match = WordBoundaryRegex(alias).Match(normalized);
                if (!match.Success) continue;
                if (match.Index > bestIndex || (match.Index == bestIndex && alias.Length > bestAliasLength))
                {
                    bestCounty = county;
                    bestIndex = match.Index;
                    bestAliasLength = alias.Length;
                }
            }
        }
        return bestCounty;
    }

    private static string Normalize(string value)
    {
        string upper = value.ToUpperInvariant().Replace('-', ' ');
        string decomposed = new(upper.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return WhitespaceRegex().Replace(decomposed, " ").Trim();
    }

    private static Regex WordBoundaryRegex(string alias) =>
        new($@"\b{Regex.Escape(alias)}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
