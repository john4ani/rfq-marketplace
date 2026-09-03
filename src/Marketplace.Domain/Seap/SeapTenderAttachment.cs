namespace Marketplace.Domain.Seap;

/// <summary>One attachment (Fișa de date, Caiet de sarcini, formular, model contract) of a SEAP tender,
/// stored in object storage or kept as a remote published reference streamed on demand. <see cref="ContentSha256"/>
/// is the SHA-256 of the actual published bytes — the document-set hash is content-based, so an authority that
/// replaces a file keeping the same URL and file name still invalidates the stored analysis.</summary>
public sealed class SeapTenderAttachment
{
    private SeapTenderAttachment() { }

    public Guid Id { get; private set; }
    public Guid TenderId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    /// <summary>Local object-storage reference or the remote e-licitatie.ro published URL (streamed on demand).</summary>
    public string S3Url { get; private set; } = string.Empty;
    /// <summary>Stable upstream document identifier from the notice-detail API (e.g. CN-W/1); used together with
    /// <see cref="ContentSha256"/> as the deterministic identity of the published document.</summary>
    public string? SourceDocumentId { get; private set; }
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

    public static SeapTenderAttachment Create(
        Guid tenderId, string fileName, string s3Url, DateTimeOffset now, string? sourceDocumentId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenderId = tenderId,
        FileName = Required(fileName, "Numele fișierului este obligatoriu."),
        S3Url = Required(s3Url, "Adresa fișierului este obligatorie."),
        SourceDocumentId = string.IsNullOrWhiteSpace(sourceDocumentId) ? null : sourceDocumentId.Trim(),
        DownloadedAt = now
    };

    /// <summary>Records the SHA-256 + size of the downloaded published bytes (idempotent — same digest is a no-op).</summary>
    public void RecordContentDigest(string contentSha256, long sizeBytes)
    {
        ContentSha256 = contentSha256;
        SizeBytes = sizeBytes;
    }

    /// <summary>Records a successful durable download of the original published bytes: switches <see cref="S3Url"/>
    /// from the remote URL (or the browsing copy) to the new Direct Hit dossier reference and stamps the content
    /// digest captured from the real stored bytes, the functional <paramref name="group"/> and the download
    /// timestamp. Idempotent — re-processing an already-<see cref="SeapDocumentDownloadStatus.Downloaded"/> file
    /// is skipped by the caller.</summary>
    public void RecordOriginalDownloaded(
        string dossierReference, string contentSha256, long sizeBytes, SeapDocumentGroup group, DateTimeOffset now)
    {
        S3Url = dossierReference;
        ContentSha256 = contentSha256;
        SizeBytes = sizeBytes;
        DocumentGroup = group;
        DownloadStatus = SeapDocumentDownloadStatus.Downloaded;
        DownloadedAt = now;
    }

    /// <summary>Records a failed durable-download attempt. The remote reference (or browsing copy) is kept so the
    /// file remains reachable on demand; the failed state lets a later DocumentImport attempt retry only the files
    /// that are not durably stored yet.</summary>
    public void RecordOriginalDownloadFailed(SeapDocumentGroup group, DateTimeOffset now)
    {
        DocumentGroup = group;
        DownloadStatus = SeapDocumentDownloadStatus.Failed;
        DownloadedAt = now;
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
