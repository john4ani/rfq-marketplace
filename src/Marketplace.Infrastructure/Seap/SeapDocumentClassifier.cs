using Marketplace.Domain.Seap;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Deterministic classifier that maps a published SEAP document to its functional <see cref="SeapDocumentGroup"/>
/// inside the Direct Hit dossier. Works from the published file name + the upstream document id only (the
/// notice-detail API gives no machine-readable section label), using Romanian procurement keywords with
/// diacritics removed so „Fișa de date" and „FISA DE DATE" classify identically. Files the keywords do not
/// match fall back to <see cref="SeapDocumentGroup.Other"/> — never an exception.
/// </summary>
internal static class SeapDocumentClassifier
{
    public static SeapDocumentGroup Classify(string? fileName, string? sourceDocumentId)
    {
        string haystack = Normalize($"{fileName} {sourceDocumentId}");

        if (ContainsAny(haystack, "clarificari", "clarification", "raspuns", "answers", "intrebari", "q&a"))
            return SeapDocumentGroup.Clarifications;
        if (ContainsAny(haystack, "formular", "formulare", "duae", "declaratie", "declaration"))
            return SeapDocumentGroup.Forms;
        if (ContainsAny(haystack, "contract"))
            return SeapDocumentGroup.ContractDraft;
        if (ContainsAny(haystack, "caiet", "fisa de date", "fisadate", "documentatie", "anexa", "anexe", "specificatii",
                "instructiuni", "instruire", "sarcini", "document", "nota", "program"))
            return SeapDocumentGroup.TenderDocumentation;
        return SeapDocumentGroup.Other;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    /// <summary>Lowercases the input and strips Romanian diacritics so keyword matching is robust to
    /// „ș/ş/Ș/Ş/ț/ț/ă/â/î" variants found in real published file names.</summary>
    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (char character in value.ToLowerInvariant())
        {
            builder.Append(character switch
            {
                'ă' or 'â' => 'a',
                'î' => 'i',
                'ș' or 'ş' => 's',
                'ț' or 'ţ' => 't',
                _ => character
            });
        }
        return builder.ToString();
    }
}
