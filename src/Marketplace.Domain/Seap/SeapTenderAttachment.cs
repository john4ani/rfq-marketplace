namespace Marketplace.Domain.Seap;

/// <summary>One attachment (Fișa de date, Caiet de sarcini, formular, DUAE, model contract) of a SEAP tender,
/// kept either as the remote e-licitatie.ro published reference (<see cref="SourceUrl"/>, streamed on demand)
/// and/or as a locally persisted object (<see cref="StorageReference"/> — an on-demand browsing copy under
/// <c>seap/{tenderId}</c> or, after a DocumentImport run, the durable original under <c>seap-originals/{tenderId}</c>).
/// The two are deliberately separate: a durably stored dossier keeps the SEAP source URL so provenance is never
/// lost. <see cref="ContentSha256"/> is the SHA-256 of the actual published bytes — the document-set hash is
/// content-based, so an authority that replaces a file keeping the same URL and file name still invalidates the
/// stored analysis.</summary>
public sealed class SeapTenderAttachment
{
    private SeapTenderAttachment() { }

    public Guid Id { get; private set; }
    public Guid TenderId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    /// <summary>The original e-licitatie.ro published URL (provenance). Null only for rows persisted before the
    /// source-URL column existed whose only reference is a locally stored copy.</summary>
    public string? SourceUrl { get; private set; }
    /// <summary>Local object-storage reference (<c>local://…</c>/<c>s3://…</c>) of a persisted copy — the browsing
    /// copy (<c>seap/{tenderId}</c>) or, after a DocumentImport run, the durable dossier original
    /// (<c>seap-originals/{tenderId}</c>).</summary>
    public string? StorageReference { get; private set; }
    /// <summary>Stable upstream document identifier from the notice-detail API (e.g. CN-W/1); used together with
    /// <see cref="ContentSha256"/> as the deterministic identity of the published document.</summary>
    public string? SourceDocumentId { get; private set; }
    /// <summary>Section label the platform grouped the document under (e.g. „Documente DUAE"), when the
    /// notice-detail payload carries one; null otherwise. Drives the dossier classification before the
    /// file-name fallback (see the classifier in the ingestion worker).</summary>
    public string? SourceSection { get; private set; }
    /// <summary>SHA-256 (hex, lowercase) of the published bytes; null until the bytes were actually downloaded
    /// (a pre-content-hash legacy row or a file whose download is still pending).</summary>
    public string? ContentSha256 { get; private set; }
    /// <summary>Size of the published bytes in bytes; null until the content was downloaded.</summary>
    public long? SizeBytes { get; private set; }
    public DateTimeOffset DownloadedAt { get; private set; }
    /// <summary>Functional group inside the Direct Hit dossier (see <see cref="SeapDocumentGroup"/>), derived
    /// deterministically from the published file name / upstream document id. <see cref="SeapDocumentGroup.None"/>
    /// until a <see cref="SeapIngestionRunPurpose.DocumentImport"/> run classified the file.</summary>
    public SeapDocumentGroup DocumentGroup { get; private set; } = SeapDocumentGroup.None;
    /// <summary>Durable-download state stamped by a <see cref="SeapIngestionRunPurpose.DocumentImport"/> run
    /// (see <see cref="SeapDocumentDownloadStatus"/>). <see cref="SeapDocumentDownloadStatus.None"/> means the
    /// file was never durably stored by a DocumentImport run — it is streamed on demand from the remote URL or
    /// from its browsing copy.</summary>
    public SeapDocumentDownloadStatus DownloadStatus { get; private set; } = SeapDocumentDownloadStatus.None;
    /// <summary>Stable machine-readable failure code of the last durable-download attempt (e.g.
    /// <c>DownloadTimeout</c>, <c>OriginalDocumentTooLarge</c>, <c>DossierQuotaExceeded</c>); null while the
    /// file is <see cref="SeapDocumentDownloadStatus.Downloaded"/> or was never processed by a DocumentImport run.</summary>
    public string? DownloadErrorCode { get; private set; }

    public static SeapTenderAttachment Create(
        Guid tenderId,
        string fileName,
        DateTimeOffset now,
        string? sourceUrl = null,
        string? storageReference = null,
        string? sourceDocumentId = null,
        string? sourceSection = null)
    {
        sourceUrl = NormalizeOptional(sourceUrl);
        storageReference = NormalizeOptional(storageReference);
        if (string.IsNullOrEmpty(sourceUrl) && string.IsNullOrEmpty(storageReference))
            throw new ArgumentException("O referință (adresă SEAP sau copie stocată) este obligatorie.");
        return new()
        {
            Id = Guid.NewGuid(),
            TenderId = tenderId,
            FileName = Required(fileName, "Numele fișierului este obligatoriu."),
            SourceUrl = sourceUrl,
            StorageReference = storageReference,
            SourceDocumentId = NormalizeOptional(sourceDocumentId),
            SourceSection = NormalizeOptional(sourceSection),
            DownloadedAt = now
        };
    }

    /// <summary>Records the SHA-256 + size of the downloaded published bytes (idempotent — same digest is a no-op).</summary>
    public void RecordContentDigest(string contentSha256, long sizeBytes)
    {
        ContentSha256 = contentSha256;
        SizeBytes = sizeBytes;
    }

    /// <summary>Records a successful durable download of the original published bytes: points
    /// <see cref="StorageReference"/> at the new Direct Hit dossier object (the SEAP <see cref="SourceUrl"/>
    /// provenance is kept untouched) and stamps the content digest captured from the real stored bytes, the
    /// functional <paramref name="group"/> and the download timestamp. Idempotent — re-processing an
    /// already-<see cref="SeapDocumentDownloadStatus.Downloaded"/> file is skipped by the caller.</summary>
    public void RecordOriginalDownloaded(
        string dossierReference, string contentSha256, long sizeBytes, SeapDocumentGroup group, DateTimeOffset now)
    {
        StorageReference = dossierReference;
        ContentSha256 = contentSha256;
        SizeBytes = sizeBytes;
        DocumentGroup = group;
        DownloadStatus = SeapDocumentDownloadStatus.Downloaded;
        DownloadErrorCode = null;
        DownloadedAt = now;
    }

    /// <summary>Records a failed durable-download attempt with an explicit machine-readable
    /// <paramref name="errorCode"/>. The source (SEAP URL or browsing copy) is kept so the file remains
    /// reachable on demand; the failed state lets a later DocumentImport attempt retry only the files that are
    /// not durably stored yet.</summary>
    public void RecordOriginalDownloadFailed(
        SeapDocumentGroup group, DateTimeOffset now, string? errorCode = null)
    {
        DocumentGroup = group;
        DownloadStatus = SeapDocumentDownloadStatus.Failed;
        DownloadErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim();
        DownloadedAt = now;
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
