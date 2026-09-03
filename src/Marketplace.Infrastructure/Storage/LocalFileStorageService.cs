using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Storage;

/// <summary>
/// Development-only storage: writes files under <c>Storage:LocalPath</c> (default
/// <c>App_Data/Storage</c>, e.g. <c>C:\CompaSpotStorage\</c>) instead of calling any cloud provider.
/// Files are organized as <c>{root}/{key}/{guid}-{fileName}</c>; the returned <c>local://</c> reference
/// mirrors the <c>s3://</c> scheme used in prod so callers do not care which provider is active.
/// </summary>
public sealed partial class LocalFileStorageService(
    IOptions<LocalStorageOptions> options,
    ILogger<LocalFileStorageService> logger) : IStorageService
{
    public async Task<string> UploadAsync(
        string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        string relativePath = Path.Combine(key, $"{Guid.NewGuid():N}-{Sanitize(fileName)}");
        string root = Path.GetFullPath(options.Value.LocalPath);
        string targetPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        try
        {
            await using (FileStream file = new(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(file, cancellationToken);
            }
        }
        catch
        {
            // A failed/cancelled copy (e.g. an over-limit document aborted by the dossier store) must never leave
            // a partial object behind — remove the half-written file and rethrow.
            try { if (File.Exists(targetPath)) File.Delete(targetPath); }
            catch { /* best-effort cleanup */ }
            throw;
        }
        LogStoredLocally(logger, relativePath);
        return $"local://{relativePath.Replace('\\', '/')}";
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken)
    {
        const string scheme = "local://";
        if (!reference.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<StorageObject?>(null);

        string root = Path.GetFullPath(options.Value.LocalPath);
        string relativePath = reference[scheme.Length..].Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        // Guard against references that escape the local storage root.
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return Task.FromResult<StorageObject?>(null);

        FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<StorageObject?>(new StorageObject(stream, StorageContentTypes.FromFileName(fullPath)));
    }

    public Task DeleteReferenceAsync(string reference, CancellationToken cancellationToken)
    {
        const string scheme = "local://";
        if (!reference.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        string root = Path.GetFullPath(options.Value.LocalPath);
        string relativePath = reference[scheme.Length..].Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        // Guard against references that escape the local storage root.
        if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 5601, Level = LogLevel.Information, Message = "File stored locally at {RelativePath}.")]
    private static partial void LogStoredLocally(ILogger logger, string relativePath);
}
