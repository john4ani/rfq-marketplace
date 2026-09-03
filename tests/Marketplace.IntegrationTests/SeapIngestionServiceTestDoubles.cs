using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Storage;

namespace Marketplace.IntegrationTests;

/// <summary>Dossier store double for tests that never exercise a DocumentImport run: any call fails loudly
/// instead of silently pretending a file was stored.</summary>
internal sealed class NullOriginalDocumentStore : ISeapOriginalDocumentStore
{
    public Task<StoredSeapOriginalDocument> StoreOriginalAsync(
        Guid tenderId, string fileName, Stream content, CancellationToken cancellationToken)
        => throw new NotSupportedException("Original-document store is not configured in this test.");

    public Task<SeapOriginalDocumentContent?> OpenOriginalAsync(string reference, CancellationToken cancellationToken)
        => throw new NotSupportedException("Original-document store is not configured in this test.");

    public Task DeleteOriginalAsync(string reference, CancellationToken cancellationToken)
        => throw new NotSupportedException("Original-document store is not configured in this test.");
}

/// <summary>Object-storage double for tests that never open/durably store files through the shared storage
/// abstraction: any call fails loudly instead of silently pretending a file exists.</summary>
internal sealed class NullObjectStorage : IStorageService
{
    public Task<string> UploadAsync(
        string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken)
        => throw new NotSupportedException("Object storage is not configured in this test.");

    public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken)
        => throw new NotSupportedException("Object storage is not configured in this test.");

    public Task DeleteReferenceAsync(string reference, CancellationToken cancellationToken)
        => throw new NotSupportedException("Object storage is not configured in this test.");
}
