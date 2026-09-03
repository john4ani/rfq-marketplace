namespace Marketplace.Domain.Seap;

/// <summary>
/// Functional group of an original SEAP published document inside the Direct Hit dossier. Derived
/// deterministically from the SEAP-provided section/type when the platform exposes one, else from the
/// published file name / upstream document id (see the classifier in the ingestion worker) so the dossier
/// is structured the same way every time, even when a document is temporarily unavailable or replaced
/// upstream. <see cref="None"/> is only the pre-classification state of rows that were never processed by
/// a <see cref="SeapIngestionRunPurpose.DocumentImport"/> run.
/// </summary>
public enum SeapDocumentGroup
{
    None = 0,
    /// <summary>Tender documentation: Fișa de date, Caietul de sarcini, documentația de atribuire, anexe.</summary>
    TenderDocumentation = 1,
    /// <summary>Documents from the platform's dedicated „Documente DUAE" section, incl. the published
    /// <c>DUAE_CERERE_*.xml</c> forms a supplier submits with the offer.</summary>
    Duae = 2,
    /// <summary>Standard forms published with the notice (formulare, declarații anexate de autoritate).</summary>
    Forms = 3,
    /// <summary>Draft contract published by the contracting authority (model contract).</summary>
    ContractDraft = 4,
    /// <summary>Clarifications / answers published after the notice.</summary>
    Clarifications = 5,
    /// <summary>Corrigenda / rectificative published after the notice.</summary>
    Corrigenda = 6,
    /// <summary>Anything the classifier cannot map deterministically to a group above.</summary>
    Other = 7
}
