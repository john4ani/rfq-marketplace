namespace Marketplace.Domain.Seap;

/// <summary>
/// Per-file durable-download state stamped by a <see cref="SeapIngestionRunPurpose.DocumentImport"/> run.
/// <see cref="None"/> is the default of rows that were never processed by a DocumentImport run (remote
/// references or browsing copies streamed on demand); <see cref="Downloaded"/> means the original bytes are
/// durably stored in the Direct Hit dossier store; <see cref="Failed"/> means the download/persist attempt
/// failed for this file (the remote reference is kept, so the file stays reachable on demand).
/// </summary>
public enum SeapDocumentDownloadStatus
{
    None = 0,
    Downloaded = 1,
    Failed = 2
}
