namespace Marketplace.Domain.Seap;

/// <summary>
/// What a SEAP ingestion run must produce. The worker executes a different stage pipeline per purpose:
/// the historical <see cref="DigestAndAnalyze"/> flow fetches the published documents, extracts readable
/// text and runs the LLM analysis (Ready = document-grounded analysis). <see cref="DocumentImport"/>
/// (Stage 1, Direct Hit dossier) only downloads and durably stores the original published documents,
/// stamps each file Downloaded/Failed and reports Ready/Partial — deliberately no text extraction and no
/// AI digest. <see cref="DocumentPreparation"/> (Stage 1.5) deterministically prepares a downloaded
/// original (the Fișa de date) into versioned pages/sections — no LLM, never inside a DocumentImport run.
/// Later stages (AI digest, reimport, P7S, offer generation) add their own purposes.
/// </summary>
public enum SeapIngestionRunPurpose
{
    /// <summary>Legacy full resolve pipeline: fetch published documents, extract text, LLM analysis.</summary>
    DigestAndAnalyze = 0,
    /// <summary>Stage 1 Direct Hit: durable download of the original published documents into the dossier
    /// store (per-file Downloaded/Failed), reporting Ready/Partial without any AI work.</summary>
    DocumentImport = 1,
    /// <summary>Stage 1.5 Direct Hit: deterministic preparation of one downloaded original document
    /// (currently the Fișa de date) into versioned pages/sections. Auto-queued after a terminal
    /// <see cref="DocumentImport"/> dossier; runs in the background, never awaited by Direct Hit polling.</summary>
    DocumentPreparation = 2
}
