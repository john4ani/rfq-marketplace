namespace Marketplace.Infrastructure.Documents;

/// <summary>
/// Cheap, dependency-free PDF sanity checks at the upload boundary. Rejects files that are obviously
/// not PDFs (missing the mandatory "%PDF-" magic header) with a clear validation error instead of
/// letting the OCR provider fail later with an unhelpful 500. Files that pass the header are still
/// validated by the extraction provider; any processing failure is non-fatal — the document stays
/// uploaded and unprocessed.
/// </summary>
public static class PdfFileValidator
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    /// <summary>True when the (seekable) upload starts with the mandatory PDF magic header.</summary>
    public static bool HasPdfHeader(Stream content)
    {
        if (!content.CanSeek || content.Length < PdfMagic.Length) return false;
        content.Position = 0;
        Span<byte> header = stackalloc byte[PdfMagic.Length];
        int read = content.Read(header);
        content.Position = 0;
        return read >= PdfMagic.Length && header.SequenceEqual(PdfMagic);
    }
}
