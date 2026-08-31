using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// File uploads go through the shared IStorageService abstraction; the S3 SDK is only referenced by
// CloudflareR2StorageService (Infrastructure). In local development Storage:UseLocalStorage swaps to
// the local file system so SEAP attachments never require R2/S3 credentials.
public sealed partial class SeapAttachmentStorage(
    IStorageService storage,
    IOptions<SeapStorageOptions> options,
    IOptions<LocalStorageOptions> localStorage,
    ILogger<SeapAttachmentStorage> logger) : ISeapAttachmentStorage
{
    private readonly SeapStorageOptions settings = options.Value;
    private readonly bool useLocalStorage = localStorage.Value.UseLocalStorage;

    public async Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (!useLocalStorage && (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Bucket)))
            throw new InvalidOperationException("Stocarea SEAP nu este configurată.");
        string reference = await storage.UploadAsync(
            settings.Bucket, $"seap/{tenderId}", Sanitize(fileName), content, DetectContentType(fileName), cancellationToken);
        LogSeapUploaded(logger, useLocalStorage);
        return reference;
    }

    private static string Sanitize(string fileName) =>
        string.Join('_', fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Best-effort content type derived from the file name (the remote SEAP documents do not expose
    /// a reliable header; the local storage layer records the type at upload).</summary>
    public static string DetectContentType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

    [LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "SEAP attachment stored (local={UseLocalStorage}).")]
    private static partial void LogSeapUploaded(ILogger logger, bool useLocalStorage);
}
