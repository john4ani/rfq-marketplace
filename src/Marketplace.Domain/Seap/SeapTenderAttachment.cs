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

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
