namespace Marketplace.Infrastructure.Documents;

public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken);
}

public sealed record DocumentExtractionResult(string FullText, IReadOnlyCollection<ExtractedPage> Pages);

public sealed record ExtractedPage(int PageNumber, string Text, IReadOnlyCollection<ExtractedTable> Tables);

public sealed record ExtractedTable(string? Caption, IReadOnlyCollection<IReadOnlyCollection<string>> Rows);
