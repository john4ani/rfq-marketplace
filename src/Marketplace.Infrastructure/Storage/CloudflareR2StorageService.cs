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
        string fullKey = $"{s3Options.Value.NormalizedRootPrefix}{key}/{Guid.NewGuid():N}/{Sanitize(fileName)}";
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = fullKey,
            InputStream = content,
            ContentType = contentType
        }, cancellationToken);
        LogUploaded(logger, fullKey);
        return $"s3://{bucket}/{fullKey}";
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    [LoggerMessage(EventId = 5602, Level = LogLevel.Information, Message = "Object uploaded to {Key}.")]
    private static partial void LogUploaded(ILogger logger, string key);
}
