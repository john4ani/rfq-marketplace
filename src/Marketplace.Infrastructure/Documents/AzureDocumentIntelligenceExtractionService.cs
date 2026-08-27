using System.Globalization;
using System.Text;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Marketplace.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace Marketplace.Infrastructure.Documents;

public sealed partial class AzureDocumentIntelligenceExtractionService : IDocumentExtractionService
{
    private readonly DocumentIntelligenceOptions settings;
    private readonly ILogger<AzureDocumentIntelligenceExtractionService> logger;
    private readonly CircuitBreaker circuitBreaker;

    public AzureDocumentIntelligenceExtractionService(
        IOptions<DocumentIntelligenceOptions> options,
        ILogger<AzureDocumentIntelligenceExtractionService> logger,
        TimeProvider timeProvider)
    {
        settings = options.Value;
        this.logger = logger;
        circuitBreaker = new CircuitBreaker(timeProvider);
    }

    public async Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Endpoint) || string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("Azure Document Intelligence nu este configurat.");
        if (circuitBreaker.IsOpen)
            throw new InvalidOperationException("Serviciul Azure Document Intelligence este temporar indisponibil (circuit deschis). Reîncearcă peste aproximativ o oră.");

        // Azure DI and PdfPig both need to re-read the stream from the start, so buffer into a seekable MemoryStream.
        using var buffer = new MemoryStream();
        await document.CopyToAsync(buffer, cancellationToken);
        byte[] pdfBytes = buffer.ToArray();

        int realPageCount = CountPdfPages(buffer);

        try
        {
            if (realPageCount <= 0)
            {
                // PdfPig could not parse the PDF; fall back to a single full-document analysis.
                AnalyzeResult full = await AnalyzeSingleAsync(pdfBytes, null, cancellationToken);
                return BuildResult(full);
            }

            // Pass 1: full-document analysis (cheap when Azure handles the whole file).
            AnalyzeResult first = await AnalyzeSingleAsync(pdfBytes, null, cancellationToken);
            int returnedPages = first.Pages.Count;

            if (returnedPages >= realPageCount)
            {
                circuitBreaker.RecordSuccess();
                int fullTextLength = ComputeFullTextLength(first);
                LogExtractionCompleted(logger, returnedPages, fullTextLength);
                return BuildResult(first);
            }

            // Pass 2: Azure truncated the response (observed on PAdES-signed / large PDFs where
            // including page 1 truncates later pages). Re-analyze page-by-page in chunks of 2 and merge.
            LogTruncationDetected(logger, returnedPages, realPageCount);
            List<ExtractedPage> merged = [];
            for (int start = 1; start <= realPageCount; start += 2)
            {
                int end = Math.Min(start + 1, realPageCount);
                AnalyzeResult chunk = await AnalyzeSingleAsync(pdfBytes, [start.ToString(CultureInfo.InvariantCulture), end.ToString(CultureInfo.InvariantCulture)], cancellationToken);
                merged.AddRange(BuildPages(chunk));
            }
            List<ExtractedPage> ordered = MergeAndOrderPages(merged);
            string fullText = string.Join('\n', ordered.Select(p => p.Text));
            circuitBreaker.RecordSuccess();
            LogExtractionCompleted(logger, ordered.Count, fullText.Length);
            return new(fullText, ordered);
        }
        catch
        {
            circuitBreaker.RecordFailure();
            throw;
        }
    }

    private async Task<AnalyzeResult> AnalyzeSingleAsync(byte[] pdfBytes, IList<string>? pages, CancellationToken cancellationToken)
    {
        var client = new DocumentAnalysisClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
        var options = new AnalyzeDocumentOptions();
        if (pages is not null)
        {
            foreach (string page in pages)
                options.Pages.Add(page);
        }
        using var stream = new MemoryStream(pdfBytes, writable: false);
        AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed, "prebuilt-layout", stream, options, cancellationToken);
        return operation.Value;
    }

    private static DocumentExtractionResult BuildResult(AnalyzeResult result) =>
        new(ComputeFullText(result), BuildPages(result));

    private static string ComputeFullText(AnalyzeResult result) =>
        string.Join('\n', result.Pages.Select(page => string.Join('\n', page.Lines.Select(line => line.Content))));

    private static int ComputeFullTextLength(AnalyzeResult result) => ComputeFullText(result).Length;

    private static List<ExtractedPage> BuildPages(AnalyzeResult result)
    {
        List<ExtractedPage> pages = [];
        foreach (DocumentPage page in result.Pages)
        {
            string text = string.Join('\n', page.Lines.Select(line => line.Content));
            List<ExtractedTable> tables = [];
            foreach (DocumentTable table in result.Tables.Where(table =>
                     table.BoundingRegions.Any(region => region.PageNumber == page.PageNumber)))
            {
                List<IReadOnlyCollection<string>> rows = [];
                foreach (IGrouping<int, DocumentTableCell> row in table.Cells.GroupBy(cell => cell.RowIndex).OrderBy(group => group.Key))
                    rows.Add(row.OrderBy(cell => cell.ColumnIndex).Select(cell => cell.Content).ToArray());
                tables.Add(new ExtractedTable(null, rows));
            }
            pages.Add(new(page.PageNumber, text, tables));
        }
        return pages;
    }

    private static int CountPdfPages(Stream document)
    {
        try
        {
            document.Position = 0;
            using PdfDocument pdf = PdfDocument.Open(document);
            return pdf.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Orders chunked extractions by page number, keeping the more complete text for duplicate pages.</summary>
    internal static List<ExtractedPage> MergeAndOrderPages(IEnumerable<ExtractedPage> chunks)
    {
        List<ExtractedPage> merged = [];
        foreach (ExtractedPage page in chunks)
        {
            int existing = merged.FindIndex(p => p.PageNumber == page.PageNumber);
            if (existing < 0)
                merged.Add(page);
            else if (page.Text.Length > merged[existing].Text.Length)
                merged[existing] = page; // keep the more complete extraction of that page
        }
        return [.. merged.OrderBy(p => p.PageNumber)];
    }

    [LoggerMessage(EventId = 5202, Level = LogLevel.Information, Message = "Document extraction completed with {PageCount} pages, {TextLength} text chars.")]
    private static partial void LogExtractionCompleted(ILogger logger, int pageCount, int textLength);

    [LoggerMessage(EventId = 5203, Level = LogLevel.Warning, Message = "Azure returned only {ReturnedPages} of {RealPages} pages; re-analyzing page-by-page and merging.")]
    private static partial void LogTruncationDetected(ILogger logger, int returnedPages, int realPages);
}
