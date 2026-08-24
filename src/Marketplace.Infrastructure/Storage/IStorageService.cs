namespace Marketplace.Infrastructure.Storage;

/// <summary>
/// Object-storage abstraction shared by every V3 file consumer (SEAP attachments, Company Vault
/// uploads, auto-bid documents). The DI wiring swaps the implementation based on
/// <c>Storage:UseLocalStorage</c>: <see cref="LocalFileStorageService"/> writes to the local file
/// system for development, <see cref="CloudflareR2StorageService"/> targets Cloudflare R2 (or AWS S3)
/// for prod/staging.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file under <paramref name="key"/> (an app-level folder path such as
    /// <c>seap/{tenderId}</c> or <c>vault/{companyId}</c>), unique-fying the stored object. Returns a
    /// stable reference URI (<c>s3://bucket/key/...</c> or <c>local://key/...</c>) that is persisted
    /// in the database. The <paramref name="bucket"/> is meaningful for the object-storage
    /// implementation only; local storage uses <paramref name="key"/> as the relative directory path.
    /// </summary>
    Task<string> UploadAsync(
        string bucket,
        string key,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
