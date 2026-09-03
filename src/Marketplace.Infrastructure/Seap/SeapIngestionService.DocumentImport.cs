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
    public const string ErrorOriginalDocumentTooLarge = "OriginalDocumentTooLarge";
    public const string ErrorDossierQuotaExceeded = "DossierQuotaExceeded";
    public const string ErrorDownloadTimeout = "DownloadTimeout";

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
        // Tender-level dossier budget (Seap:MaxTenderOriginalDocumentsBytes): the bytes durably stored for this
        // tender are tracked across the run (existing Downloaded originals count too), so a run can never grow
        // the dossier past the configured bound even when documents are large or numerous.
        long dossierCap = settings.MaxTenderOriginalDocumentsBytes;
        long alreadyStoredBytes = tender.Attachments
            .Where(attachment => attachment.DownloadStatus == SeapDocumentDownloadStatus.Downloaded)
            .Sum(attachment => attachment.SizeBytes ?? 0);
        DossierBudget? budget = dossierCap > 0 ? new DossierBudget(alreadyStoredBytes, dossierCap) : null;
        Task[] jobs = pending
            .Select(attachment => DownloadOriginalAsync(tender.Id, attachment, gate, budget, now, cancellationToken))
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
    /// timeout, the per-file size limit (enforced inside the dossier store) and the run-wide concurrency gate.
    /// A per-file failure (timeout, over-limit file, quota exceeded, SEAP hiccup, storage error) marks ONLY that
    /// attachment Failed with an explicit error code and never fails the whole run — the dossier keeps the files
    /// that did store and the source reference of the failed one for on-demand streaming.</summary>
    private async Task DownloadOriginalAsync(
        Guid tenderId, SeapTenderAttachment attachment, SemaphoreSlim gate, DossierBudget? budget,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            SeapDocumentGroup group = attachment.DocumentGroup == SeapDocumentGroup.None
                ? SeapDocumentClassifier.Classify(attachment.SourceSection, attachment.FileName, attachment.SourceDocumentId)
                : attachment.DocumentGroup;

            // The tender-level dossier quota is already exhausted — the file cannot be stored; skip the download
            // instead of wasting the network round-trip.
            if (budget is not null && budget.IsExhausted)
            {
                attachment.RecordOriginalDownloadFailed(group, now, ErrorDossierQuotaExceeded);
                LogOriginalDossierQuotaExceeded(logger, attachment.FileName);
                return;
            }

            try
            {
                using var fileTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                fileTimeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.DocumentDownloadTimeoutSeconds)));
                await using Stream content = await OpenAttachmentContentAsync(attachment, fileTimeout.Token);
                StoredSeapOriginalDocument stored =
                    await originalDocumentStore.StoreOriginalAsync(tenderId, attachment.FileName, content, fileTimeout.Token);

                // Enforce the whole-dossier cap AFTER the store (the real size is only known once the bytes
                // streamed): a file that pushes the dossier over the bound is deleted again — the dossier never
                // silently grows past the configured maximum — and marked Failed so a later run can retry it.
                if (budget is not null && budget.AddAndGetTotal(stored.SizeBytes) > budget.CapBytes)
                {
                    budget.Rollback(stored.SizeBytes);
                    await originalDocumentStore.DeleteOriginalAsync(stored.Reference, fileTimeout.Token);
                    attachment.RecordOriginalDownloadFailed(group, now, ErrorDossierQuotaExceeded);
                    LogOriginalDossierQuotaExceeded(logger, attachment.FileName);
                    return;
                }

                attachment.RecordOriginalDownloaded(stored.Reference, stored.ContentSha256, stored.SizeBytes, group, now);
            }
            catch (SeapOriginalDocumentTooLargeException exception)
            {
                // Over the per-file size cap — the dossier store removed any partially written object before
                // surfacing this exception, so nothing needs cleaning here.
                attachment.RecordOriginalDownloadFailed(group, now, ErrorOriginalDocumentTooLarge);
                LogOriginalDocumentTooLarge(logger, attachment.FileName, exception);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Per-file download timeout (or the storage upload timed out) — never a run-level failure.
                attachment.RecordOriginalDownloadFailed(group, now, ErrorDownloadTimeout);
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

    /// <summary>Streams the attachment content: a locally persisted copy (browsing copy under <c>seap/…</c> or an
    /// earlier dossier original) is read from object storage; a row that only carries the remote e-licitatie.ro
    /// URL is downloaded live. Every Direct Hit original ends up in the same logically separate, durable dossier
    /// namespace (<c>seap-originals/{tenderId}</c>).</summary>
    private async Task<Stream> OpenAttachmentContentAsync(SeapTenderAttachment attachment, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(attachment.StorageReference))
        {
            StorageObject? stored = await objectStorage.OpenReadAsync(attachment.StorageReference, cancellationToken);
            if (stored is not null) return stored.Content;
        }

        if (!string.IsNullOrWhiteSpace(attachment.SourceUrl))
            return await apiClient.DownloadAttachmentAsync(attachment.SourceUrl, cancellationToken);

        throw new InvalidOperationException($"Sursa nu este disponibilă pentru {attachment.FileName} (nici copie stocată, nici adresă SEAP).");
    }

    [LoggerMessage(EventId = 5151, Level = LogLevel.Warning, Message = "SEAP original document {FileName} hit the download timeout; marked Failed for this run.")]
    private static partial void LogOriginalDownloadTimedOut(ILogger logger, string fileName);
    [LoggerMessage(EventId = 5152, Level = LogLevel.Warning, Message = "SEAP original document {FileName} could not be downloaded/stored durably; marked Failed for this run.")]
    private static partial void LogOriginalDownloadFailed(ILogger logger, string fileName, Exception exception);
    [LoggerMessage(EventId = 5154, Level = LogLevel.Warning, Message = "SEAP original document {FileName} exceeds the per-file dossier size limit; marked Failed (OriginalDocumentTooLarge).")]
    private static partial void LogOriginalDocumentTooLarge(ILogger logger, string fileName, Exception exception);
    [LoggerMessage(EventId = 5155, Level = LogLevel.Warning, Message = "SEAP original document {FileName} was not stored because the tender dossier quota is exceeded; marked Failed (DossierQuotaExceeded).")]
    private static partial void LogOriginalDossierQuotaExceeded(ILogger logger, string fileName);

    /// <summary>Thread-safe running total of the bytes durably stored in one tender's dossier. The parallel
    /// download jobs (bounded by the run-wide concurrency gate) add their real stored sizes after each upload and
    /// roll back when the whole-dossier cap is crossed and the object is deleted again.</summary>
    private sealed class DossierBudget
    {
        private readonly object gate = new();
        private long total;

        public DossierBudget(long initialBytes, long capBytes)
        {
            total = initialBytes;
            CapBytes = capBytes;
        }

        public long CapBytes { get; }

        public bool IsExhausted
        {
            get { lock (gate) { return total >= CapBytes; } }
        }

        public long AddAndGetTotal(long sizeBytes)
        {
            lock (gate) { total += sizeBytes; return total; }
        }

        public void Rollback(long sizeBytes)
        {
            lock (gate) { total = Math.Max(0, total - sizeBytes); }
        }
    }
}
