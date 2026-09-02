namespace Marketplace.Domain.Seap;

/// <summary>
/// How fully the content of the published document set was verified through content digests.
/// <c>None</c> — the notice publishes no documents; <c>Partial</c> — at least one document could not be
/// downloaded/hashed (analysis is grounded in the documents that WERE readable, but not in the whole dossier);
/// <c>Complete</c> — every published document has a captured SHA-256 content digest.
/// </summary>
public enum SeapDocumentDigestStatus
{
    None = 0,
    Partial = 1,
    Complete = 2
}
