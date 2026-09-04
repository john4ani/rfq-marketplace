namespace Marketplace.Domain.Seap;

/// <summary>
/// Deterministic role of a published SEAP original document inside the Direct Hit dossier. Distinct from
/// <see cref="SeapDocumentGroup"/> (the dossier shelf the file is filed under): the role is what the file IS,
/// so the pipeline can decide which preparer applies (a Fișa de date is prepared by the deterministic
/// data-sheet extractor; DUAE/contract/forms get their own handling later). <see cref="Unknown"/> is the
/// pre-classification state; every file classified by the DocumentImport stage receives a concrete role.
/// </summary>
public enum SeapDocumentRole
{
    Unknown = 0,
    /// <summary>Generated „Fișa de date" PDF (<c>Instructiuni_ofertanti_FisaDate_DF*.pdf</c>) — the official,
    /// no-.p7s structured source of the notice details (Stage 1.5 deterministic preparation).</summary>
    DataSheet = 1,
    /// <summary>Published DUAE form / document (the platform's dedicated „Documente DUAE" section).</summary>
    Duae = 2,
    /// <summary>Caietul de sarcini / technical specification.</summary>
    TechnicalSpecification = 3,
    /// <summary>Standard forms published with the notice (formulare, declarații).</summary>
    Forms = 4,
    /// <summary>Draft contract / model contract published by the authority.</summary>
    ContractDraft = 5,
    /// <summary>Technical project documentation (PTH, planuri, memorii tehnice, devize).</summary>
    TechnicalProject = 6,
    /// <summary>Anything the deterministic classifier cannot map to a concrete role above.</summary>
    Other = 7
}
