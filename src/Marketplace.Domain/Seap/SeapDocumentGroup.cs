namespace Marketplace.Domain.Seap;

/// <summary>
/// Functional group of an original SEAP published document inside the Direct Hit dossier. Derived
/// deterministically from the published file name / upstream document id (see the classifier in the
/// ingestion worker) so the dossier is structured the same way every time, even when a document is
/// temporarily unavailable or replaced upstream. <see cref="None"/> is only the pre-classification state
/// of rows that were never processed by a <see cref="SeapIngestionRunPurpose.DocumentImport"/> run.
/// </summary>
public enum SeapDocumentGroup
{
    None = 0,
    /// <summary>Tender documentation: Fișa de date, Caietul de sarcini, documentația de atribuire, anexe.</summary>
    TenderDocumentation = 1,
    /// <summary>Standard forms published with the notice (formulare, DUAE, declarații anexate de autoritate).</summary>
    Forms = 2,
    /// <summary>Draft contract published by the contracting authority (model contract).</summary>
    ContractDraft = 3,
    /// <summary>Clarifications / answers published after the notice.</summary>
    Clarifications = 4,
    /// <summary>Anything the classifier cannot map deterministically to a group above.</summary>
    Other = 5
}
