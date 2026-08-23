namespace Marketplace.Domain.Vault;

/// <summary>
/// A company's document vault (1-to-0..1 with SupplierProfile / Company). A vault exists only
/// when a company has uploadable documents; it is a separate table, never an owned value object.
/// </summary>
public sealed class CompanyVault
{
    private readonly List<VaultDocument> _documents = [];
    private CompanyVault() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<VaultDocument> Documents => _documents;

    public static CompanyVault Create(Guid supplierId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        SupplierId = supplierId,
        CreatedAt = now,
        UpdatedAt = now
    };

    public VaultDocument AddDocument(string fileName, string s3Url, VaultDocumentType documentType, Guid uploadedBy, DateTimeOffset now)
    {
        VaultDocument document = VaultDocument.Create(Id, fileName, s3Url, documentType, uploadedBy, now);
        _documents.Add(document);
        UpdatedAt = now;
        return document;
    }
}
