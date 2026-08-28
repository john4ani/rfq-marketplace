using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Marketplace.Domain.Seap;

/// <summary>
/// Deterministic county extraction from a SEAP contracting-authority name (e.g.
/// "17095927 - DIRECTIA GENERALA DE ASISTENTA SOCIALA SI PROTECTIA COPILULUI VASLUI" → "Vaslui",
/// "MUNICIPIUL CLUJ-NAPOCA" → "Cluj", "COMUNA GIURGIU" → "Giurgiu"). No AI is needed: authority names
/// routinely end with the county seat, the county itself, or a locality of the county. Matching is
/// layered and deterministic: 1) county names + residence-city aliases; 2) an explicit county code after
/// "JUD" (e.g. "PRIMARIA COMUNEI MARGINEA, JUD. SV" → "Suceava"); 3) a unique locality name under a
/// local-administration marker such as "COMUNA"/"ORASULUI"/"PRIMARIA" (e.g. "PRIMARIA ORASULUI DOROHOI"
/// → "Botoșani"). Returns null when no county is recognizable.
/// </summary>
public static partial class CountyParser
{
    private static readonly JsonSerializerOptions LocalityJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Local-administration markers that qualify a locality name as the contracting authority's
    /// home commune/town (e.g. "PRIMARIA COMUNEI MARGINEA"). Without a marker a company name is never
    /// classified by a matching locality word (a "SC X SA" must not turn into a county).</summary>
    private static readonly string[] LocalityMarkers =
        ["COMUNA", "COMUNEI", "ORAS", "ORASUL", "ORASULUI", "MUNICIPIUL", "MUNICIPIULUI", "PRIMARIA",
         "CONSILIUL LOCAL", "SATUL", "LOCALITATEA"];

    /// <summary>Folded locality name → canonical county, loaded from the embedded
    /// <c>locality-counties.json</c> resource. Contains only names that map to exactly one county
    /// nationwide, so a commune/town can never be attributed to the wrong county.</summary>
    private static Dictionary<string, string> LocalityCounty { get; } = LoadLocalityCounty();

    private static Dictionary<string, string> LoadLocalityCounty()
    {
        using Stream? stream = typeof(CountyParser).Assembly
            .GetManifestResourceStream("Marketplace.Domain.Seap.locality-counties.json");
        return stream is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(stream, LocalityJsonOptions)
                ?? new Dictionary<string, string>();
    }
    /// <summary>County name with its aliases (county name + residence city, all WITHOUT diacritics). The
    /// authoritative table lives in <see cref="RomanianCountyCodes.CountyAliases"/> so the import parser
    /// and the SEAP Tender Match normalizer share exactly one vocabulary — e.g. "CLUJ NAPOCA" and
    /// "Cluj-Napoca" both normalize to Cluj, "TARGU MURES" and "Târgu-Mureș" both to Mureș.</summary>
    private static (string County, string[] Aliases)[] CountyAliases => RomanianCountyCodes.CountyAliases;

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
        if (bestCounty is not null) return bestCounty;

        // Fallbacks for authorities that name a locality instead of the county: an explicit county code
        // after "JUD" wins, then a unique locality under a local-administration marker.
        return MatchCountyCode(normalized) ?? MatchLocalityCounty(normalized);
    }

    /// <summary>Matches an explicit county code written as "JUD. SV", "JUD SV", "JUDETUL SV" (the codes are
    /// unambiguous — "SV" can only mean Suceava), so "PRIMARIA COMUNEI MARGINEA, JUD. SV" → Suceava even
    /// when the commune name alone would be ambiguous.</summary>
    private static string? MatchCountyCode(string normalized)
    {
        Match match = CountyCodeRegex().Match(normalized);
        return match.Success ? RomanianCountyCodes.TryNormalize(match.Groups["code"].Value) : null;
    }

    /// <summary>Scans the name for the last locality phrase (up to three words) that maps to exactly one
    /// county, but only when a local-administration marker is present.</summary>
    private static string? MatchLocalityCounty(string normalized)
    {
        if (LocalityCounty.Count == 0) return null;
        if (!LocalityMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal))) return null;

        string[] tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? bestCounty = null;
        int bestTokenIndex = -1;
        int bestPhraseLength = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            for (int length = 3; length >= 1; length--)
            {
                if (i + length > tokens.Length) continue;
                string phrase = string.Join(' ', tokens, i, length);
                if (LocalityCounty.TryGetValue(phrase, out string? county))
                {
                    if (i > bestTokenIndex || (i == bestTokenIndex && length > bestPhraseLength))
                    {
                        bestCounty = county;
                        bestTokenIndex = i;
                        bestPhraseLength = length;
                    }
                    break;
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

    [GeneratedRegex(@"\bJUD(?:ETUL|ET)?\.?\s*(?<code>[A-Z]{1,2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex CountyCodeRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
