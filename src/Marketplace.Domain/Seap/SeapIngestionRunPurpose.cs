namespace Marketplace.Domain.Seap;

/// <summary>
/// What a SEAP ingestion run must produce. The worker executes a different stage pipeline per purpose:
/// the historical <see cref="DigestAndAnalyze"/> flow fetches the published documents, extracts readable
/// text and runs the LLM analysis (Ready = document-grounded analysis). <see cref="DocumentImport"/>
/// (Stage 1, Direct Hit dossier) only downloads and durably stores the original published documents,
/// stamps each file Downloaded/Failed and reports Ready/Partial — deliberately no text extraction and no
/// AI digest. Later stages (AI digest, reimport, P7S, offer generation) add their own purposes.
/// </summary>
public enum SeapIngestionRunPurpose
{
    /// <summary>Legacy full resolve pipeline: fetch published documents, extract text, LLM analysis.</summary>
    DigestAndAnalyze = 0,
    /// <summary>Stage 1 Direct Hit: durable download of the original published documents into the dossier
    /// store (per-file Downloaded/Failed), reporting Ready/Partial without any AI work.</summary>
    DocumentImport = 1
}
