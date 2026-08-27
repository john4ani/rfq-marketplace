using Marketplace.Domain.Vault;

namespace Marketplace.Application.Companies;

public sealed record VaultDocumentView(
    Guid Id,
    string FileName,
    string DocumentType,
    bool Extracted,
    int? PageCount,
    int ChunkCount,
    DateTimeOffset UploadedAt);

/// <summary>The two proposal documents generated from a Caiet de sarcini.</summary>
public enum GeneratedBidFileKind { TechnicalProposal, BillOfQuantities }

/// <summary>A generated proposal file opened for download.</summary>
public sealed record VaultDownload(Stream Content, string FileName, string ContentType);

public interface ICompanyVaultService
{
    Task<IReadOnlyCollection<VaultDocumentView>> GetVaultAsync(Guid companyId, CancellationToken cancellationToken);
    Task<VaultDocumentView?> UploadDocumentAsync(Guid companyId, string fileName, Stream content, VaultDocumentType documentType, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Opens a generated proposal file (.docx/.xlsx) for download, or <c>null</c> when missing.</summary>
    Task<VaultDownload?> DownloadGeneratedFileAsync(
        Guid companyId, Guid documentId, GeneratedBidFileKind kind, CancellationToken cancellationToken);
}
