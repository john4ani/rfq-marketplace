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

public interface ICompanyVaultService
{
    Task<IReadOnlyCollection<VaultDocumentView>> GetVaultAsync(Guid companyId, CancellationToken cancellationToken);
    Task<VaultDocumentView?> UploadDocumentAsync(Guid companyId, string fileName, Stream content, VaultDocumentType documentType, Guid actorId, CancellationToken cancellationToken);
}
