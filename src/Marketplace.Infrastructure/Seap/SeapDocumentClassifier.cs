using Marketplace.Domain.Seap;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Deterministic classifier that maps a published SEAP document to its functional <see cref="SeapDocumentGroup"/>
/// inside the Direct Hit dossier. Classification is done PRIORITARILY from the section/type label the platform
/// grouped the document under (e.g. the dedicated „Documente DUAE" section shown on real notice pages), and only
/// as a fallback from the published file name + the upstream document id — the observed
/// <c>GetDfNoticeSectionFiles</c> payload does not carry a section today, so real runs classify from the file
/// name. Romanian procurement keywords are matched with diacritics removed so „Fișa de date" and „FISA DE DATE"
/// classify identically and the published <c>DUAE_CERERE_*.xml</c> forms always land in
/// <see cref="SeapDocumentGroup.Duae"/>. Files the keywords do not match fall back to
/// <see cref="SeapDocumentGroup.Other"/> — never an exception.
/// </summary>
internal static class SeapDocumentClassifier
{
    public static SeapDocumentGroup Classify(string? section, string? fileName, string? sourceDocumentId)
    {
        // 1) SEAP-provided section/type wins whenever the platform exposes one.
        string sectionHaystack = Normalize(section);
        if (sectionHaystack.Length > 0)
        {
            SeapDocumentGroup group = ClassifyBySection(sectionHaystack);
            if (group != SeapDocumentGroup.Other)
                return group;
        }

        // 2) File-name (and upstream code) fallback, diacritics-insensitive.
        string haystack = Normalize($"{fileName} {sourceDocumentId}");
        if (ContainsAny(haystack, "duae_cerere", "duae-cerere", "duae.cerere") || StartsWithAny(haystack, "duae_"))
            return SeapDocumentGroup.Duae;
        if (ContainsAny(haystack, "clarificari", "clarification", "raspuns", "answers", "intrebari", "q&a"))
            return SeapDocumentGroup.Clarifications;
        if (ContainsAny(haystack, "corigenda", "corigendum", "corrigenda", "rectificare", "rectificativ", "erata"))
            return SeapDocumentGroup.Corrigenda;
        if (ContainsAny(haystack, "duae"))
            return SeapDocumentGroup.Duae;
        if (ContainsAny(haystack, "formular", "formulare", "declaratie", "declaration"))
            return SeapDocumentGroup.Forms;
        if (ContainsAny(haystack, "contract"))
            return SeapDocumentGroup.ContractDraft;
        if (ContainsAny(haystack, "caiet", "fisa de date", "fisadate", "documentatie", "anexa", "anexe", "specificatii",
                "instructiuni", "instruire", "sarcini", "document", "nota", "program"))
            return SeapDocumentGroup.TenderDocumentation;
        return SeapDocumentGroup.Other;
    }

    /// <summary>Priority matching on the machine-readable section/type label. An unrecognized section falls
    /// through to the file-name rules below rather than mis-classifying as Other.</summary>
    private static SeapDocumentGroup ClassifyBySection(string section)
    {
        if (ContainsAny(section, "duae"))
            return SeapDocumentGroup.Duae;
        if (ContainsAny(section, "clarificari", "clarification", "intrebari", "raspuns", "q&a"))
            return SeapDocumentGroup.Clarifications;
        if (ContainsAny(section, "corigenda", "corrigenda", "rectificare", "rectificativ", "erata"))
            return SeapDocumentGroup.Corrigenda;
        if (ContainsAny(section, "formular", "formulare", "declaratie", "declaration"))
            return SeapDocumentGroup.Forms;
        if (ContainsAny(section, "contract"))
            return SeapDocumentGroup.ContractDraft;
        if (ContainsAny(section, "documentatie", "caiet", "fisa", "fisa de date", "anexa", "specificatii", "sarcini"))
            return SeapDocumentGroup.TenderDocumentation;
        return SeapDocumentGroup.Other;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static bool StartsWithAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.StartsWith(needle, StringComparison.Ordinal));

    /// <summary>Lowercases the input and strips Romanian diacritics so keyword matching is robust to
    /// „ș/ş/Ș/Ş/ț/ț/ă/â/î" variants found in real published file names.</summary>
    private static string Normalize(string? value)
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

