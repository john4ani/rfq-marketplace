using System.Globalization;
using System.Text;

namespace Marketplace.Domain.Seap;

/// <summary>
/// Deterministic ISO 3166-2:RO county-code ↔ canonical-name mapping used by the SEAP Tender Match
/// interpreter and the Anunțuri SEAP filters. The database stores canonical county names (e.g. "Iași"),
/// not codes ("IS"), so every LLM-extracted code is normalized to its canonical name before it is
/// persisted or used in the LINQ filter.
/// </summary>
public static class RomanianCountyCodes
{
    private static readonly IReadOnlyDictionary<string, string> CodeToCounty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AB"] = "Alba", ["AR"] = "Arad", ["AG"] = "Argeș", ["BC"] = "Bacău", ["BH"] = "Bihor",
            ["BN"] = "Bistrița-Năsăud", ["BT"] = "Botoșani", ["BV"] = "Brașov", ["BR"] = "Brăila",
            ["B"] = "București", ["BZ"] = "Buzău", ["CS"] = "Caraș-Severin", ["CL"] = "Călărași",
            ["CJ"] = "Cluj", ["CT"] = "Constanța", ["CV"] = "Covasna", ["DB"] = "Dâmbovița",
            ["DJ"] = "Dolj", ["GL"] = "Galați", ["GR"] = "Giurgiu", ["GJ"] = "Gorj", ["HR"] = "Harghita",
            ["HD"] = "Hunedoara", ["IL"] = "Ialomița", ["IS"] = "Iași", ["IF"] = "Ilfov",
            ["MM"] = "Maramureș", ["MH"] = "Mehedinți", ["MS"] = "Mureș", ["NT"] = "Neamț",
            ["OT"] = "Olt", ["PH"] = "Prahova", ["SJ"] = "Sălaj", ["SM"] = "Satu Mare",
            ["SB"] = "Sibiu", ["SV"] = "Suceava", ["TR"] = "Teleorman", ["TM"] = "Timiș",
            ["TL"] = "Tulcea", ["VS"] = "Vaslui", ["VL"] = "Vâlcea", ["VN"] = "Vrancea"
        };

    /// <summary>The 42 canonical county names, ordered, matching the CountyParser vocabulary.</summary>
    public static IReadOnlyCollection<string> AllCounties { get; } = CodeToCounty.Values
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Residence-city (and spelling) aliases per county, uppercase and diacritics-free (hyphens as
    /// spaces). Shared with <see cref="CountyParser"/> so a value such as "Târgu-Mureș" or "Cluj-Napoca"
    /// normalizes to the same canonical county the tender import produces ("Mureș", "Cluj") — the
    /// authoritative alias vocabulary lives here exactly once.
    /// </summary>
    public static readonly (string County, string[] Aliases)[] CountyAliases =
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

    /// <summary>
    /// Returns the canonical county name for a code (e.g. "IS" → "Iași"), for a value that is already a
    /// county name (diacritics-insensitive, e.g. "IASI" or "Iasi" → "Iași"), or for a residence-city alias
    /// of a county (e.g. "Cluj-Napoca" → "Cluj", "Târgu-Mureș" → "Mureș") — the same alias vocabulary the
    /// SEAP tender import uses. Returns null when the value is not a recognized Romanian county.
    /// </summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string trimmed = value.Trim();
        if (CodeToCounty.TryGetValue(trimmed, out string? byCode)) return byCode;

        string folded = Fold(trimmed);
        foreach (string county in AllCounties)
            if (Fold(county) == folded) return county;
        foreach ((string county, string[] aliases) in CountyAliases)
            foreach (string alias in aliases)
                if (alias == folded) return county;
        return null;
    }

    private static string Fold(string value)
    {
        string upper = value.ToUpperInvariant().Replace('-', ' ');
        string decomposed = new(upper.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return string.Join(' ', decomposed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
