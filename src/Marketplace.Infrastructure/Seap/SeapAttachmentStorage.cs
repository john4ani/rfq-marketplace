using Amazon.S3;
using Amazon.S3.Model;
using Marketplace.Application.Seap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// S3 uploads go through this interface; the AWS SDK is only referenced here (Infrastructure).
public sealed partial class SeapAttachmentStorage(
    IAmazonS3 s3,
    IOptions<SeapStorageOptions> options,
    ILogger<SeapAttachmentStorage> logger) : ISeapAttachmentStorage
{
    private readonly SeapStorageOptions settings = options.Value;

    public async Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Bucket))
            throw new InvalidOperationException("Stocarea SEAP nu este configurată.");
        string key = $"seap/{tenderId}/{Guid.NewGuid():N}/{Sanitize(fileName)}";
        PutObjectRequest request = new()
        {
            BucketName = settings.Bucket,
            Key = key,
            InputStream = content,
            ContentType = DetectContentType(fileName)
        };
        await s3.PutObjectAsync(request, cancellationToken);
        LogSeapUploaded(logger, key);
        return $"s3://{settings.Bucket}/{key}";
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    private static string DetectContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "SEAP attachment uploaded to {Key}.")]
    private static partial void LogSeapUploaded(ILogger logger, string key);
}
