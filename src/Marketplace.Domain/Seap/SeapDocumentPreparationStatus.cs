namespace Marketplace.Domain.Seap;

/// <summary>
/// Lifecycle of one deterministic document-preparation result (<see cref="SeapDocumentPreparation"/>).
/// <see cref="Pending"/> until a worker claims the run; terminal states never change afterwards.
/// </summary>
public enum SeapDocumentPreparationStatus
{
    Pending = 0,
    Preparing = 1,
    Prepared = 2,
    /// <summary>Text was extracted but with warnings / coverage below the threshold (or the PDF has no usable
    /// text layer — then <c>NeedsOcr</c> is set on the preparation). Never an error.</summary>
    PartiallyPrepared = 3,
    /// <summary>The published file is in a format the deterministic preparer does not support.</summary>
    Unsupported = 4,
    Failed = 5
}
