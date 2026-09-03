using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Stage 1 (DocumentImport) run pipeline: downloads the ORIGINAL published documents of a Direct Hit tender
/// into the durable dossier store (<see cref="ISeapOriginalDocumentStore"/>, logically separate namespace),
/// stamps each attachment Downloaded/Failed + its functional <see cref="SeapDocumentGroup"/>, then completes
/// the run Ready (all originals stored), Partial (some stored, some failed) or Failed (nothing could be
/// stored). Deliberately NO text extraction and NO LLM analysis — the AI digest is a later stage and never
/// runs inside a DocumentImport run.
/// </summary>
public sealed partial class SeapIngestionService
{
    public const string ErrorDocumentsDownloadFailed = "DocumentsDownloadFailed";

    private async Task ProcessDocumentImportRunAsync(
        SeapIngestionRun run, SeapTender tender, RunClaim claim, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Only files that are not durably stored yet are processed — retries and repeat runs never
        // re-download an already-<see cref="SeapDocumentDownloadStatus.Downloaded"/> original.
        SeapTenderAttachment[] pending = tender.Attachments
            .Where(attachment => attachment.DownloadStatus != SeapDocumentDownloadStatus.Downloaded)
            .ToArray();

        int maxConcurrent = Math.Max(1, settings.MaxConcurrentDocumentDownloads);
        using var gate = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        Task[] jobs = pending
            .Select(attachment => DownloadOriginalAsync(tender.Id, attachment, gate, now, cancellationToken))
            .ToArray();
        await Task.WhenAll(jobs);

        int downloaded = tender.Attachments.Count(attachment => attachment.DownloadStatus == SeapDocumentDownloadStatus.Downloaded);
        int failed = tender.Attachments.Count(attachment => attachment.DownloadStatus == SeapDocumentDownloadStatus.Failed);
        tender.RecordDocumentDigestStatus(ComputeDigestStatus(tender.Attachments), now);
        tender.RecordDocumentSetHash(ComputeDocumentSetHash(tender.Attachments), now);

        if (tender.Attachments.Count == 0)
        {
            // The notice published no documents OR the list could not be fetched right now — the two cannot be
            // told apart (the client degrades a failed fetch to an empty list). Mark the run Partial with the
            // same NoPublishedDocuments code the digest pipeline uses for this state, never a false Ready.
            run.Complete(SeapIngestionStatus.Partial, now, ErrorNoPublishedDocuments,
                "Anunțul nu publică documente de descărcat (sau lista nu a putut fi obținută); dosarul rămâne fără documente.");
            LogSeapRunCompleted(logger, run.Id, SeapIngestionStatus.Partial);
        }
        else if (failed == 0)
        {
            run.Complete(SeapIngestionStatus.Ready, now);
            LogSeapRunCompleted(logger, run.Id, SeapIngestionStatus.Ready);
        }
        else if (downloaded == 0)
        {
            run.Complete(SeapIngestionStatus.Failed, now, ErrorDocumentsDownloadFailed,
                $"Toate cele {tender.Attachments.Count} documente publicate nu au putut fi descărcate și stocate durabil.");
            LogSeapRunCompleted(logger, run.Id, SeapIngestionStatus.Failed);
        }
        else
        {
            run.Complete(SeapIngestionStatus.Partial, now, ErrorDocumentsDownloadFailed,
                $"{failed} din {tender.Attachments.Count} documente nu au putut fi descărcate; {downloaded} au fost stocate durabil.");
            LogSeapRunCompleted(logger, run.Id, SeapIngestionStatus.Partial);
        }

        await FencedSaveAsync(run, claim, now, cancellationToken);
    }

    /// <summary>Downloads + durably stores one original published document, honoring the per-file download
    /// timeout and the run-wide concurrency gate. A per-file failure (timeout, SEAP hiccup, storage error)
    /// marks ONLY that attachment Failed and never fails the whole run — the dossier keeps the files that
    /// did store and the remote reference of the failed one for on-demand streaming.</summary>
    private async Task DownloadOriginalAsync(
        Guid tenderId, SeapTenderAttachment attachment, SemaphoreSlim gate, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            SeapDocumentGroup group = attachment.DocumentGroup == SeapDocumentGroup.None
                ? SeapDocumentClassifier.Classify(attachment.FileName, attachment.SourceDocumentId)
                : attachment.DocumentGroup;
            try
            {
                using var fileTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                fileTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.DocumentDownloadTimeoutSeconds)));
                await using Stream content = await OpenAttachmentContentAsync(attachment, fileTimeout.Token);
                StoredSeapOriginalDocument stored =
                    await originalDocumentStore.StoreOriginalAsync(tenderId, attachment.FileName, content, fileTimeout.Token);
                attachment.RecordOriginalDownloaded(stored.Reference, stored.ContentSha256, stored.SizeBytes, group, now);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Per-file download timeout (or the storage upload timed out) — never a run-level failure.
                attachment.RecordOriginalDownloadFailed(group, now);
                LogOriginalDownloadTimedOut(logger, attachment.FileName);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                attachment.RecordOriginalDownloadFailed(group, now);
                LogOriginalDownloadFailed(logger, attachment.FileName, exception);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Streams the attachment content: remote e-licitatie.ro URL (published-document reference) or the
    /// currently persisted storage copy (browsing copy) — the latter is re-stored under the dossier namespace so
    /// every Direct Hit original ends up in the same logically separate, durable location.</summary>
    private async Task<Stream> OpenAttachmentContentAsync(SeapTenderAttachment attachment, CancellationToken cancellationToken)
    {
        if (attachment.S3Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return await apiClient.DownloadAttachmentAsync(attachment.S3Url, cancellationToken);

        StorageObject? stored = await objectStorage.OpenReadAsync(attachment.S3Url, cancellationToken);
        return stored?.Content
            ?? throw new InvalidOperationException($"Copie locală indisponibilă pentru {attachment.FileName}.");
    }

    [LoggerMessage(EventId = 5151, Level = LogLevel.Warning, Message = "SEAP original document {FileName} hit the download timeout; marked Failed for this run.")]
    private static partial void LogOriginalDownloadTimedOut(ILogger logger, string fileName);
    [LoggerMessage(EventId = 5152, Level = LogLevel.Warning, Message = "SEAP original document {FileName} could not be downloaded/stored durably; marked Failed for this run.")]
    private static partial void LogOriginalDownloadFailed(ILogger logger, string fileName, Exception exception);
}
