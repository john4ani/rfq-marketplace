using System.Text;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Marketplace.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        try
        {
            var client = new DocumentAnalysisClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.ApiKey));
            AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed, "prebuilt-layout", document, cancellationToken: cancellationToken);
            AnalyzeResult result = operation.Value;

            List<ExtractedPage> pages = [];
            StringBuilder fullText = new();
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
                fullText.AppendLine(text);
            }
            circuitBreaker.RecordSuccess();
            LogExtractionCompleted(logger, pages.Count);
            return new(fullText.ToString(), pages);
        }
        catch
        {
            circuitBreaker.RecordFailure();
            throw;
        }
    }

    [LoggerMessage(EventId = 5202, Level = LogLevel.Information, Message = "Document extraction completed with {PageCount} pages.")]
    private static partial void LogExtractionCompleted(ILogger logger, int pageCount);
}
