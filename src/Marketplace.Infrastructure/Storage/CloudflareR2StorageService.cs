using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Storage;

/// <summary>
/// Object-storage implementation for prod/staging: uploads through the shared <see cref="IAmazonS3"/>
/// client, which targets Cloudflare R2 when <c>S3:ServiceUrl</c> is set (path-style + static
/// credentials) and AWS S3 via the default IAM chain otherwise. Object keys carry the configured
/// environment root prefix (<c>prod/</c> vs <c>staging/</c>) so both environments share one bucket.
/// </summary>
public sealed partial class CloudflareR2StorageService(
    IAmazonS3 s3,
    IOptions<S3Options> s3Options,
    ILogger<CloudflareR2StorageService> logger) : IStorageService
{
    public async Task<string> UploadAsync(
        string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // R2 requires full-payload SigV4 signing (chunked streaming is not implemented), so the SDK must
        // compute the payload hash up front; buffer non-seekable streams so the length/hash are available.
        using MemoryStream buffer = new();
        if (content.CanSeek) content.Position = 0;
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        string fullKey = $"{s3Options.Value.NormalizedRootPrefix}{key}/{Guid.NewGuid():N}/{Sanitize(fileName)}";
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = fullKey,
            InputStream = buffer,
            ContentType = contentType,
            // R2 has no SigV4 streaming implementation; sign with the full payload hash instead.
            UseChunkEncoding = false
        }, cancellationToken);
        LogUploaded(logger, fullKey);
        return $"s3://{bucket}/{fullKey}";
    }

    public async Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken)
    {
        const string scheme = "s3://";
        if (!reference.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return null;

        string path = reference[scheme.Length..];
        int separator = path.IndexOf('/');
        if (separator <= 0) return null;

        try
        {
            GetObjectResponse response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = path[..separator],
                Key = path[(separator + 1)..]
            }, cancellationToken);
            string contentType = response.Headers.ContentType ?? StorageContentTypes.FromFileName(path);
            return new StorageObject(response.ResponseStream, contentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            LogObjectMissing(logger, path);
            return null;
        }
    }

    public async Task DeleteReferenceAsync(string reference, CancellationToken cancellationToken)
    {
        const string scheme = "s3://";
        if (!reference.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            return;

        string path = reference[scheme.Length..];
        int separator = path.IndexOf('/');
        if (separator <= 0) return;

        await s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = path[..separator],
            Key = path[(separator + 1)..]
        }, cancellationToken);
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    [LoggerMessage(EventId = 5602, Level = LogLevel.Information, Message = "Object uploaded to {Key}.")]
    private static partial void LogUploaded(ILogger logger, string key);
    [LoggerMessage(EventId = 5603, Level = LogLevel.Warning, Message = "Object {Key} not found for download.")]
    private static partial void LogObjectMissing(ILogger logger, string key);
}
