namespace Marketplace.Domain.Vault;

public enum VaultDocumentType { Specification, TechnicalProposal, Certificate, FinancialStatement, Other }

/// <summary>One uploaded vault document. Content lives in S3; the vault stores the URI and OCR/embedding state.</summary>
public sealed class VaultDocument
{
    private readonly List<DocumentChunk> _chunks = [];
    private VaultDocument() { }

    public Guid Id { get; private set; }
    public Guid VaultId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string S3Url { get; private set; } = string.Empty;
    public VaultDocumentType DocumentType { get; private set; }
    public bool Extracted { get; private set; }
    public int? PageCount { get; private set; }
    /// <summary>Markdown text extracted from the source PDF by Azure Document Intelligence (prebuilt-layout).</summary>
    public string? ExtractedMarkdown { get; private set; }
    /// <summary>Auto-bid proposal generated from the specification (Caiet de sarcini).</summary>
    public string? ProposalTitle { get; private set; }
    public string? TechnicalProposalUrl { get; private set; }
    public string? BillOfQuantitiesUrl { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public IReadOnlyCollection<DocumentChunk> Chunks => _chunks;

    public static VaultDocument Create(Guid vaultId, string fileName, string s3Url, VaultDocumentType documentType, Guid uploadedBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        VaultId = vaultId,
        FileName = Required(fileName, "Numele fișierului este obligatoriu."),
        S3Url = Required(s3Url, "Adresa fișierului este obligatorie."),
        DocumentType = documentType,
        UploadedBy = uploadedBy,
        UploadedAt = now
    };

    public DocumentChunk AddChunk(int chunkIndex, string content, float[]? embedding, DateTimeOffset now)
    {
        DocumentChunk chunk = DocumentChunk.Create(Id, chunkIndex, content, embedding, now);
        _chunks.Add(chunk);
        return chunk;
    }

    public void MarkExtracted(int? pageCount)
    {
        Extracted = true;
        PageCount = pageCount;
    }

    public void SetExtractedMarkdown(string markdown) =>
        ExtractedMarkdown = string.IsNullOrWhiteSpace(markdown) ? null : markdown.Trim();

    public void RecordGeneratedProposal(string title, string technicalUrl, string billOfQuantitiesUrl)
    {
        ProposalTitle = Required(title, "Titlul propunerii este obligatoriu.");
        TechnicalProposalUrl = Required(technicalUrl, "Adresa propunerii tehnice este obligatorie.");
        BillOfQuantitiesUrl = Required(billOfQuantitiesUrl, "Adresa devizului este obligatorie.");
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
