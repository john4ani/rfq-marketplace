using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Reads readable text from a SEAP tender's published attachments (Fișa de date, Caiet de sarcini) so the
/// on-demand „Sinteză AI" generation can be grounded in the real document content instead of the lean
/// notice-search metadata. Best-effort: PDFs (PdfPig text layer), DOCX (Open XML) and plain text are read
/// directly; ZIP archives are scanned for readable members. Scanned/unreadable formats and failed downloads
/// are skipped (never a gate) — <see cref="ReadPublishedTextAsync"/> returns null when no attachment yields text.
/// </summary>
public sealed partial class SeapAttachmentTextReader(
    ISeapApiClient apiClient,
    IStorageService storage,
    ILogger<SeapAttachmentTextReader> logger,
    IOptions<SeapOptions>? seapOptions = null) : ISeapAttachmentTextReader
{
    private const int MaxBytesPerFile = 30 * 1024 * 1024;
    private const int MaxTotalCharacters = 150_000;
    private const int MaxArchiveEntries = 80;
    private readonly int documentDownloadTimeoutSeconds = seapOptions?.Value.DocumentDownloadTimeoutSeconds ?? 60;

    public async Task<SeapAttachmentReadResult> ReadPublishedTextAsync(
        IReadOnlyCollection<SeapTenderAttachment> attachments, CancellationToken cancellationToken)
    {
        var extracted = new StringBuilder();
        var digests = new List<SeapAttachmentContentDigest>();
        foreach (SeapTenderAttachment attachment in attachments)
        {
            try
            {
                // Per-file document download timeout (distinct from the whole-pass extraction timeout). A
                // timeout propagates as a cancellation so the run pipeline retries/backs off — never a
                // suspended call.
                byte[] content = await ReadAttachmentBytesBoundedAsync(attachment.S3Url, cancellationToken);
                if (content.Length > 0)
                {
                    // Content digest is captured from the REAL downloaded bytes, so replacing a file upstream
                    // (same URL + file name) changes the digest and invalidates the stored analysis.
                    byte[] hash = SHA256.HashData(content);
                    digests.Add(new SeapAttachmentContentDigest(
                        attachment.FileName, attachment.S3Url,
                        Convert.ToHexString(hash).ToLowerInvariant(), content.LongLength));
                }
                string? text = ReadText(content, attachment.FileName);
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (extracted.Length > 0) extracted.Append('\n');
                extracted.Append("=== ").Append(attachment.FileName).Append(" ===\n").Append(text.Trim());
                if (extracted.Length >= MaxTotalCharacters) break;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A per-file download hit its own timeout → let it surface so the run is retried (not hung).
                throw;
            }
            catch (Exception exception)
            {
                LogAttachmentReadFailed(logger, attachment.FileName, exception);
            }
        }

        if (extracted.Length > MaxTotalCharacters)
            extracted.Length = MaxTotalCharacters;
        string? result = extracted.ToString();
        return new SeapAttachmentReadResult(
            string.IsNullOrWhiteSpace(result) ? null : result, digests);
    }

    private async Task<byte[]> ReadAttachmentBytesBoundedAsync(string reference, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, documentDownloadTimeoutSeconds)));
        try
        {
            return await ReadAttachmentBytesAsync(reference, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw; // per-file download timeout → the run pipeline turns it into a bounded retry
        }
    }

    private async Task<byte[]> ReadAttachmentBytesAsync(string reference, CancellationToken cancellationToken)
    {
        if (reference.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            await using Stream remote = await apiClient.DownloadAttachmentAsync(reference, cancellationToken);
            return await ReadBoundedAsync(remote, cancellationToken);
        }

        StorageObject? stored = await storage.OpenReadAsync(reference, cancellationToken);
        if (stored is null) return [];
        await using Stream content = stored.Content;
        return await ReadBoundedAsync(content, cancellationToken);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81_920];
        int total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            int remaining = MaxBytesPerFile - total;
            if (remaining <= 0) break;
            int written = Math.Min(read, remaining);
            buffer.Write(chunk, 0, written);
            total += written;
        }
        return buffer.ToArray();
    }

    private string? ReadText(byte[] content, string fileName)
    {
        string extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => ReadPdfText(content),
            ".docx" => ReadDocxText(content),
            ".txt" => Encoding.UTF8.GetString(content).Trim(),
            ".zip" => ReadZipText(content),
            // SEAP publishes PAdES/CAdES-signed files whose names end in .p7s. The payload may be a plain PDF
            // (PAdES) or a CMS SignedData wrapper around a PDF/DOCX/ZIP/text — unwrap and read the inner content.
            ".p7s" => ReadP7sText(content),
            _ => null
        };
    }

    private static bool LooksLikePdf(byte[] content) =>
        content.Length >= 5 && content[0] == '%' && content[1] == 'P' && content[2] == 'D' && content[3] == 'F' && content[4] == '-';

    private static bool LooksLikeZip(byte[] content) =>
        content.Length > 4 && content[0] == 'P' && content[1] == 'K';

    /// <summary>Reads a PAdES/CAdES <c>.p7s</c> file: a plain PDF payload is read directly, otherwise the CMS
    /// SignedData wrapper is unwrapped and the embedded content (PDF/DOCX/ZIP/plain text) is read.</summary>
    private string? ReadP7sText(byte[] content)
    {
        byte[] candidate = LooksLikePdf(content) ? content : TryUnwrapCms(content) ?? content;
        if (LooksLikePdf(candidate)) return ReadPdfText(candidate);
        if (LooksLikeZip(candidate))
        {
            try
            {
                string? docx = ReadDocxText(candidate);
                if (!string.IsNullOrWhiteSpace(docx)) return docx;
            }
            catch
            {
                // not a DOCX — fall through to the ZIP member scan
            }
            return ReadZipText(candidate);
        }
        string? plain = Encoding.UTF8.GetString(candidate).Trim();
        return string.IsNullOrWhiteSpace(plain) ? null : plain;
    }

    private static byte[]? TryUnwrapCms(byte[] content)
    {
        try
        {
            var signed = new SignedCms();
            signed.Decode(content);
            byte[] inner = signed.ContentInfo.Content;
            return inner is { Length: > 0 } ? inner : null;
        }
        catch
        {
            return null; // not a CMS SignedData payload (or a format we cannot decode)
        }
    }

    private static string? ReadPdfText(byte[] content)
    {
        if (!LooksLikePdf(content)) return null;
        using PdfDocument document = PdfDocument.Open(content);
        var builder = new StringBuilder();
        foreach (Page page in document.GetPages())
        {
            builder.Append(page.Text).Append('\n');
            if (builder.Length >= MaxTotalCharacters) break;
        }
        return builder.Length == 0 ? null : builder.ToString().Trim();
    }

    private static string? ReadDocxText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        IEnumerable<Paragraph> paragraphs = document.MainDocumentPart?.Document?.Body?.Descendants<Paragraph>() ?? [];
        string text = string.Join('\n', paragraphs.Select(paragraph => paragraph.InnerText));
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private string? ReadZipText(byte[] content)
    {
        using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
        var builder = new StringBuilder();
        int scannedEntries = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (scannedEntries++ >= MaxArchiveEntries) break;
            string extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (extension is not (".pdf" or ".docx" or ".txt")) continue;

            byte[] entryBytes = ReadBoundedZipEntry(entry);
            string? text = ReadText(entryBytes, entry.Name);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (builder.Length > 0) builder.Append('\n');
            builder.Append("=== ").Append(entry.FullName).Append(" ===\n").Append(text.Trim());
            if (builder.Length >= MaxTotalCharacters) break;
        }
        return builder.Length == 0 ? null : builder.ToString().Trim();
    }

    private static byte[] ReadBoundedZipEntry(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var buffer = new MemoryStream();
        int maxBytes = entry.Length > 0 ? (int)Math.Min(entry.Length, MaxBytesPerFile) : MaxBytesPerFile;
        byte[] chunk = new byte[16_384];
        int total = 0;
        int read;
        while ((read = stream.Read(chunk, 0, Math.Min(chunk.Length, maxBytes - total))) > 0)
        {
            buffer.Write(chunk, 0, read);
            total += read;
            if (total >= maxBytes) break;
        }
        return buffer.ToArray();
    }

    [LoggerMessage(EventId = 5130, Level = LogLevel.Warning, Message = "SEAP published attachment {FileName} could not be read for the synthesis; it is skipped.")]
    private static partial void LogAttachmentReadFailed(ILogger logger, string fileName, Exception exception);
}

