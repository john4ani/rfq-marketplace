using System.Security.Cryptography;
using System.Text;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Marketplace.Infrastructure.Seap;

/// <summary>Livrarea 2/2.1 — resumable run pipeline with four concurrency guarantees: (1) FENCING — each claim
/// bumps <see cref="SeapIngestionRun.ClaimVersion"/> and every persisted checkpoint re-reads the run row
/// (SELECT … FOR UPDATE) and aborts unless the worker still owns ClaimedBy+ClaimVersion, so a stale worker
/// can never overwrite the reclaiming worker; (2) EFFECTIVE HEARTBEAT — <see cref="RenewActiveLeasesAsync"/>
/// renews the lease of every run claimed by this worker instance while downloads/OCR/LLM run; (3) ATTEMPT
/// LIMIT — <see cref="SeapOptions.MaxIngestionAttempts"/> bounds claims; retryable failures stay ACTIVE with
/// jittered backoff and only become Failed after the last allowed attempt, while terminal Partial is reserved
/// for legitimately incomplete results; (4) IDEMPOTENCY — checkpoints and their results are persisted in the
/// same transaction, the document-set hash is a deterministic function of a sorted identifier list, and every
/// LLM attempt carries a fresh AnalysisAttemptId + stable AnalysisRequestHash (at-least-once execution with
/// idempotent persistence; duplicate external calls are minimized but not exactly-once).</summary>
public sealed partial class SeapIngestionService
{
    public const string ErrorNoPublishedDocuments = "NoPublishedDocuments";
    public const string ErrorNoReadableDocuments = "NoReadableDocuments";
    public const string ErrorAnalysisFailed = "AnalysisFailed";
    public const string ErrorMaxAttempts = "MaxAttemptsReached";

    private sealed record RunClaim(string WorkerId, long ClaimVersion, int AttemptCount);

    // Process-wide registry of the runs this process is actually processing: heartbeat renewals are limited to
    // its entries, so an abandoned processing task cannot be kept alive by the heartbeat.
    private static readonly SeapActiveRunRegistry RunRegistry = new();

    public async Task<int> ProcessPendingIngestionRunsAsync(string workerId, int maxRuns, CancellationToken cancellationToken)
    {
        if (maxRuns <= 0) return 0;
        int maxAttempts = Math.Max(1, settings.MaxIngestionAttempts);
        int processed = 0;

        // Candidate list is only a hint: each run is claimed individually, immediately before processing, so
        // a claimed run never waits in a batch while its lease expires.
        DateTimeOffset now0 = timeProvider.GetUtcNow();
        List<Guid> candidates = await dbContext.SeapIngestionRuns.AsNoTracking()
            .Where(run => ActiveIngestionStatuses.Contains(run.Status)
                && (run.LeaseExpiresAt == null || run.LeaseExpiresAt < now0)
                && (run.NextAttemptAt == null || run.NextAttemptAt <= now0)
                && run.AttemptCount < maxAttempts)
            .OrderBy(run => run.CreatedAt)
            .Take(maxRuns)
            .Select(run => run.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid runId in candidates)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateTimeOffset leaseUntil = now.AddSeconds(Math.Max(1, settings.RunLeaseSeconds));

            // Atomic claim: lease check + AttemptCount++ + ClaimVersion++ in one statement.
            int claimed = await dbContext.SeapIngestionRuns
                .Where(run => run.Id == runId
                    && (run.LeaseExpiresAt == null || run.LeaseExpiresAt < now)
                    && (run.NextAttemptAt == null || run.NextAttemptAt <= now))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(run => run.ClaimedBy, workerId)
                    .SetProperty(run => run.LeaseExpiresAt, leaseUntil)
                    .SetProperty(run => run.LastHeartbeatAt, now)
                    .SetProperty(run => run.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(run => run.AttemptCount, run => run.AttemptCount + 1)
                    .SetProperty(run => run.ClaimVersion, run => run.ClaimVersion + 1)
                    .SetProperty(run => run.UpdatedAt, now),
                    cancellationToken);
            if (claimed == 0) continue;

            RunClaim claim = new(workerId,
                await dbContext.SeapIngestionRuns.AsNoTracking().Where(run => run.Id == runId)
                    .Select(run => run.ClaimVersion).SingleAsync(cancellationToken),
                await dbContext.SeapIngestionRuns.AsNoTracking().Where(run => run.Id == runId)
                    .Select(run => run.AttemptCount).SingleAsync(cancellationToken));
            processed++;

            try
            {
                await ProcessClaimedRunAsync(runId, claim, leaseUntil, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Graceful host shutdown: the lease expires naturally and another worker resumes the run.
                break;
            }
            catch (SeapRunLeaseLostException)
            {
                // Another worker reclaimed the run after our lease expired; abandon without touching it.
            }
            catch (SeapRunTransientException exception)
            {
                // Retryable pipeline failure (LLM timeout/429/5xx, invalid schema): schedule a bounded,
                // attempt-limited retry — never an immediate terminal Partial.
                await ScheduleRunRetryAsync(runId, exception.ErrorCode, exception.Message, now, cancellationToken);
            }
            catch (Exception exception)
            {
                await ScheduleRunRetryAsync(runId, "RunProcessingFailed", exception.Message, now, cancellationToken);
            }
        }
        return processed;
    }

    /// <summary>Advances one claimed run through the persistent checkpoints. Each stage mutates the tracked
    /// entities and persists result + checkpoint in a single fenced transaction, so a crash between stages
    /// leaves the previous stage durably committed and the next claim resumes from the first gap.</summary>
    /// <summary>Claimed-run wrapper: registers the run in the active-run registry (so the heartbeat may renew
    /// its lease only while it is really being processed), runs the stage pipeline and always unregisters the
    /// run when processing ends — success, retryable failure, lease lost or host shutdown. The in-memory touch
    /// loop keeps the registry entry fresh for the whole duration of the pipeline (including long LLM calls).</summary>
    private async Task ProcessClaimedRunAsync(
        Guid runId, RunClaim claim, DateTimeOffset leaseUntil, DateTimeOffset now, CancellationToken cancellationToken)
    {
        RunRegistry.Add(runId, claim.WorkerId, claim.ClaimVersion, timeProvider.GetUtcNow());
        using var heartbeatCts = new CancellationTokenSource();
        Task touchLoop = Task.Run(() => TouchActiveRunLoopAsync(runId, heartbeatCts.Token), CancellationToken.None);
        try
        {
            await ProcessClaimedRunCoreAsync(runId, claim, leaseUntil, now, cancellationToken);
        }
        finally
        {
            heartbeatCts.Cancel();
            try { await touchLoop; }
            catch (OperationCanceledException) { }
            catch { /* the touch loop must never mask pipeline results */ }
            // Removes only our own claim (version-guarded), never the entry of a worker that reclaimed the run.
            RunRegistry.Remove(runId, claim.ClaimVersion);
        }
    }

    private async Task TouchActiveRunLoopAsync(Guid runId, CancellationToken cancellationToken)
    {
        int intervalMilliseconds = Math.Max(250, settings.RunHeartbeatIntervalSeconds * 1000);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(intervalMilliseconds, cancellationToken);
                if (!RunRegistry.Touch(runId, timeProvider.GetUtcNow())) return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // normal end
        }
    }

    private async Task ProcessClaimedRunCoreAsync(
        Guid runId, RunClaim claim, DateTimeOffset leaseUntil, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // The claim advanced the run row through ExecuteUpdate on the database. A context that already tracked
        // this run (same instance reused across ticks, as in tests) would otherwise keep the pre-claim
        // ClaimVersion and fail every fenced save, so we reload the row with the fresh fencing token.
        dbContext.ChangeTracker.Clear();
        SeapIngestionRun run = await dbContext.SeapIngestionRuns
            .SingleAsync(item => item.Id == runId, cancellationToken);
        run.SetStage(SeapIngestionStatus.FetchingDocuments, now);
        await FencedSaveAsync(run, claim, now, cancellationToken);

        SeapTender tender = await dbContext.SeapTenders
            .Include(item => item.Attachments)
            .SingleAsync(item => item.Id == run.TenderId, cancellationToken);

        // Stage 1 — resolve the numeric NoticeId when the imported row predates it (live call; a failure here
        // surfaces as SeapSourceUnavailableException → retryable, attempt-limited).
        if (tender.NoticeId is null)
        {
            SeapTenderNoticeDto? notice = await RunWithSeapTimeoutAsync(
                "SeapSearchTimeout", settings.SearchTimeoutSeconds,
                token => apiClient.FetchNoticeByNumberAsync(tender.SeapExternalId, token), cancellationToken);
            if (notice is null)
            {
                tender.RecordIngestionFailure("NoticeUnavailableOnSeap", "Anunțul nu mai este disponibil pe SEAP.", now);
                run.Complete(SeapIngestionStatus.Failed, now, "NoticeUnavailableOnSeap", "Anunțul nu mai este disponibil pe SEAP.");
                await FencedSaveAsync(run, claim, now, cancellationToken);
                return;
            }
            tender.BackfillNoticeSource(notice.NoticeId, notice.SysNoticeTypeId, now);
            await FencedSaveAsync(run, claim, now, cancellationToken);
        }

        // Stage 2 — resolve + attach published documents and stamp the deterministic DocumentSetHash. Result
        // and DocumentsFetchedAt checkpoint commit together, so DocumentsFetchedAt != null always means the
        // attachment metadata + hash are durably persisted (a crash before the commit leaves a recoverable
        // state that resumes without duplicates — new attachments are deduplicated by file name).
        if (run.DocumentsFetchedAt is null || tender.Attachments.Count == 0)
        {
            IReadOnlyCollection<SeapNoticeDocumentDto> documents = await RunWithSeapTimeoutAsync(
                "DocumentDownloadTimeout", settings.DocumentDownloadTimeoutSeconds,
                token => apiClient.FetchNoticeDocumentsAsync(tender.NoticeId!.Value, tender.SysNoticeTypeId ?? 2, token), cancellationToken);
            foreach (SeapNoticeDocumentDto document in documents)
            {
                // De-duplicate by the stable upstream code first (the DB unique index on
                // (TenderId, SourceDocumentId) backs this up); file name is the legacy fallback for rows that
                // were attached before codes existed.
                bool known = tender.Attachments.Any(attachment =>
                    (!string.IsNullOrWhiteSpace(document.Code)
                        && string.Equals(attachment.SourceDocumentId, document.Code, StringComparison.Ordinal))
                    || string.Equals(attachment.FileName, document.FileName, StringComparison.OrdinalIgnoreCase));
                if (known)
                    continue;
                dbContext.Add(tender.AddAttachment(document.FileName, now,
                    sourceUrl: document.DownloadUrl, sourceDocumentId: document.Code, sourceSection: document.Section));
            }
            tender.RecordDocumentSetHash(ComputeDocumentSetHash(tender.Attachments), now);
            tender.RecordDocumentDigestStatus(ComputeDigestStatus(tender.Attachments), now);
            run.RecordDocumentsFetched(now);
            await FencedSaveAsync(run, claim, now, cancellationToken);
        }

        // Stage 2b — DocumentImport purpose (Stage 1, Direct Hit dossier): durably download and store the
        // original published documents into the dedicated dossier namespace, stamp every file Downloaded/Failed
        // + its functional group, then complete Ready/Partial. This purpose deliberately skips text extraction
        // and the LLM analysis (the AI digest belongs to a later stage and never runs inside a DocumentImport
        // run). Non-DocumentImport runs continue through the digest/analysis pipeline below.
        if (run.Purpose == SeapIngestionRunPurpose.DocumentImport)
        {
            await ProcessDocumentImportRunAsync(run, tender, claim, now, cancellationToken);
            return;
        }

        // Stage 3 — extract readable text AND capture the SHA-256 of the real published bytes. The content
        // digests are stamped on the attachments and the content-based DocumentSetHash is recomputed, all in
        // the SAME fenced transaction as the TextExtractedAt checkpoint — so an authority replacing a file
        // (same URL + file name, different bytes) produces a different set hash and invalidates old analyses.
        string? attachmentText = run.ExtractedText;
        if (run.TextExtractedAt is null)
        {
            if (tender.Attachments.Count > 0)
            {
                SeapAttachmentReadResult read = await RunWithSeapTimeoutAsync(
                    "TextExtractionTimeout", settings.TextExtractionTimeoutSeconds,
                    token => attachmentTextReader.ReadPublishedTextAsync(tender.Attachments, token), cancellationToken);
                attachmentText = read.Text;
                foreach (SeapAttachmentContentDigest digest in read.Digests)
                {
                    SeapTenderAttachment? match = tender.Attachments.FirstOrDefault(attachment =>
                        string.Equals(attachment.FileName, digest.FileName, StringComparison.OrdinalIgnoreCase));
                    match?.RecordContentDigest(digest.ContentSha256, digest.SizeBytes);
                }
            }
            else
            {
                attachmentText = null;
            }
            tender.RecordDocumentDigestStatus(ComputeDigestStatus(tender.Attachments), now);
            tender.RecordDocumentSetHash(ComputeDocumentSetHash(tender.Attachments), now);
            run.RecordTextExtracted(attachmentText, now);
            await FencedSaveAsync(run, claim, now, cancellationToken);
        }

        bool documentsAvailable = tender.Attachments.Count > 0;

        // Stage 4 — LLM analysis. The fresh attempt id + stable request hash are persisted before the call,
        // then the call runs (at-least-once; a lost response cannot be recovered so every retry is a new
        // attempt). Retryable failures never become terminal Partial before the attempt limit.
        run.SetStage(SeapIngestionStatus.Analyzing, now);
        run.BeginAnalysis(ComputeAnalysisRequestHash(tender), now);
        await FencedSaveAsync(run, claim, now, cancellationToken);

        SeapAiExtraction? extraction = await RunWithSeapTimeoutAsync(
            "AnalysisTimeout", settings.LlmTimeoutSeconds,
            token => summaryGenerator.GenerateAsync(tender, attachmentText, token), cancellationToken);
        if (extraction is null)
        {
            if (!documentsAvailable)
            {
                // No published document and the analysis call is unavailable → legitimately incomplete; the
                // reason is terminal (no auto retry), the notice stays available with its raw metadata.
                tender.RecordIngestionFailure(ErrorNoPublishedDocuments, "Nu au fost găsite documente publicate pentru acest anunț.", now);
                run.Complete(SeapIngestionStatus.Partial, now, ErrorNoPublishedDocuments, "Nu au fost găsite documente publicate pentru acest anunț.");
                LogSeapRunCompleted(logger, runId, SeapIngestionStatus.Partial);
                await FencedSaveAsync(run, claim, now, cancellationToken);
                return;
            }
            throw new SeapRunTransientException(ErrorAnalysisFailed,
                "Analiza AI nu a produs un rezultat valid (timeout, limită sau eroare tranzitorie).");
        }

        if (attachmentText is not null)
        {
            tender.RecordDocumentGroundedAnalysis(
                extraction, tender.DocumentSetHash ?? string.Empty, Math.Max(1, settings.AnalysisVersion), now);
            if (tender.ImportMode != SeapImportMode.DirectHistoricalLookup && tender.Status == SeapTenderStatus.Imported)
                tender.MarkReadyForBidding(now);
            run.Complete(SeapIngestionStatus.Ready, now);
            LogSeapRunCompleted(logger, runId, SeapIngestionStatus.Ready);
        }
        else
        {
            // Documents exist but none is readable (scanned/password-protected/corrupt): metadata-only
            // extraction is the best result → terminal Partial, no auto retry.
            tender.ApplyAiExtraction(extraction, now);
            tender.RecordIngestionFailure(ErrorNoReadableDocuments,
                "Documentele publicate nu au text lizibil (scan/parolă). Anunțul rămâne disponibil cu datele de bază.", now);
            run.Complete(SeapIngestionStatus.Partial, now, ErrorNoReadableDocuments,
                "Documentele publicate nu au text lizibil (scan/parolă).");
            LogSeapRunCompleted(logger, runId, SeapIngestionStatus.Partial);
        }
        await FencedSaveAsync(run, claim, now, cancellationToken);
    }

    /// <summary>Lease heartbeat. Renews ONLY the runs this worker instance is ACTUALLY processing (they are in
    /// <see cref="SeapActiveRunRegistry"/> with a fresh last-seen timestamp), and only when the conditional
    /// update still matches RunId + WorkerId + ClaimVersion + an active status. A run whose processing task
    /// ended — even unexpectedly, without a terminal state — disappears from the registry, so the heartbeat
    /// stops renewing it and its lease expires for another worker to reclaim (no „zombie runs").</summary>
    public async Task<int> RenewActiveLeasesAsync(string workerId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset leaseUntil = now.AddSeconds(Math.Max(1, settings.RunLeaseSeconds));
        DateTimeOffset staleBefore = now.Subtract(TimeSpan.FromSeconds(Math.Max(10, settings.RunHeartbeatIntervalSeconds * 3)));

        int renewed = 0;
        foreach (SeapActiveRunEntry entry in RunRegistry.GetActiveFor(workerId))
        {
            if (entry.LastSeenAt < staleBefore) continue; // processing no longer touching → do not renew
            int affected = await dbContext.SeapIngestionRuns
                .Where(run => run.Id == entry.RunId
                    && run.ClaimedBy == entry.WorkerId
                    && run.ClaimVersion == entry.ClaimVersion
                    && ActiveIngestionStatuses.Contains(run.Status))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(run => run.LeaseExpiresAt, leaseUntil)
                    .SetProperty(run => run.LastHeartbeatAt, now),
                    cancellationToken);
            renewed += affected;
        }
        return renewed;
    }

    /// <summary>Schedules a bounded retry for a run that failed transiently: the run stays ACTIVE (a retryable
    /// LLM/SEAP failure is never terminal Partial), the lease is released and NextAttemptAt is set to a
    /// jittered exponential backoff (capped by MaxRetryDelayMinutes). After the last allowed attempt the run
    /// becomes Failed with CompletedAt set, releasing its global capacity slot.</summary>
    private async Task ScheduleRunRetryAsync(Guid runId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.ChangeTracker.Clear();
            SeapIngestionRun? run = await dbContext.SeapIngestionRuns
                .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
            if (run is null || run.ClaimedBy is null) return;
            RunClaim claim = new(run.ClaimedBy, run.ClaimVersion, run.AttemptCount);

            int maxAttempts = Math.Max(1, settings.MaxIngestionAttempts);
            if (run.AttemptCount >= maxAttempts)
            {
                run.Complete(SeapIngestionStatus.Failed, now, errorCode, errorMessage);
                LogSeapRunPermanentFailure(logger, runId, errorCode, maxAttempts);
                await FencedSaveAsync(run, claim, now, cancellationToken);
                return;
            }

            double capSeconds = TimeSpan.FromMinutes(Math.Max(1, settings.MaxRetryDelayMinutes)).TotalSeconds;
            double baseSeconds = Math.Max(1, settings.RunRetryBaseDelaySeconds) * Math.Pow(2, Math.Min(run.AttemptCount - 1, 5));
            double jitter = 0.8 + (Random.Shared.NextDouble() * 0.4); // ±20% — workers do not retry in lockstep
            DateTimeOffset nextAttemptAt = now.AddSeconds(Math.Min(capSeconds, baseSeconds) * jitter);
            run.ReleaseForRetry(errorCode, errorMessage, nextAttemptAt, now);
            LogSeapRunRetryScheduled(logger, runId, errorCode, nextAttemptAt);
            await FencedSaveAsync(run, claim, now, cancellationToken);
        }
        catch (Exception exception)
        {
            LogSeapRunRetryFailed(logger, runId, exception);
        }
    }

    /// <summary>Fenced persistence: every checkpoint/result is committed inside a transaction whose run UPDATE
    /// carries <c>WHERE ClaimVersion = &lt;claim version&gt;</c> (optimistic concurrency token). If the run was
    /// reclaimed while this worker was busy (lease expired + another claim bumped the version), zero rows are
    /// affected and the WHOLE transaction — including any attachment inserts and tender updates staged in the
    /// same SaveChanges — rolls back; the stale worker throws <see cref="SeapRunLeaseLostException"/> and can
    /// never overwrite the reclaiming worker's progress or final analysis.</summary>
    private async Task FencedSaveAsync(SeapIngestionRun run, RunClaim claim, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new SeapRunLeaseLostException(run.Id);
            }
            catch
            {
                throw;
            }
        });
    }

    /// <summary>Deterministic, content-based hash of the resolved document set: sorted, de-duplicated list of
    /// <c>upstream document id + SHA-256 of the actual published bytes</c>. Two files with the same URL and
    /// file name but different bytes hash differently. When the bytes were never downloaded (legacy row or a
    /// still-pending download) the stable upstream document id is used as a temporary identity — never the
    /// URL/file name alone, which the authority can keep unchanged while swapping the content.</summary>
    private static string ComputeDocumentSetHash(IReadOnlyCollection<SeapTenderAttachment> attachments)
    {
        string[] entries = attachments
            .Select(attachment =>
            {
                string identity = attachment.SourceDocumentId ?? attachment.SourceUrl ?? attachment.StorageReference ?? string.Empty;
                string content = attachment.ContentSha256 ?? "content-unknown";
                return $"{identity}|{content}";
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
        string payload = string.Join("\n", entries);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>Stable hash of the analysis request: notice number + content-based document-set hash +
    /// analysis-version + prompt/schema/model descriptor. Identical across retries of the same logical
    /// analysis with the same logic version; different when the document set OR the analysis logic changed.</summary>
    private string ComputeAnalysisRequestHash(SeapTender tender) =>
        ComputeAnalysisRequestHash(tender, settings.AnalysisVersion, settings.AnalysisLogicDescriptor);

    private static string ComputeAnalysisRequestHash(SeapTender tender, int analysisVersion, string logicDescriptor)
    {
        string payload = $"{tender.SeapExternalId}|{tender.DocumentSetHash ?? string.Empty}|{analysisVersion}|{logicDescriptor}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    /// <summary>Runs one external call under a distinct per-operation timeout. A timeout (or any cancellation
    /// that is not the host's) surfaces as <see cref="SeapRunTransientException"/> → the normal retry/backoff
    /// path; the pipeline never suspends a call without a bound.</summary>
    private static async Task<T> RunWithSeapTimeoutAsync<T>(
        string errorCode, int timeoutSeconds, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
        try
        {
            return await operation(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SeapRunTransientException(errorCode, $"Apelul extern a depășit limita de {timeoutSeconds} s.");
        }
    }

    /// <summary>Completeness of the content verification for the current document set.</summary>
    private static SeapDocumentDigestStatus ComputeDigestStatus(IReadOnlyCollection<SeapTenderAttachment> attachments)
    {
        if (attachments.Count == 0) return SeapDocumentDigestStatus.None;
        return attachments.All(attachment => !string.IsNullOrWhiteSpace(attachment.ContentSha256))
            ? SeapDocumentDigestStatus.Complete
            : SeapDocumentDigestStatus.Partial;
    }

    private sealed class SeapRunLeaseLostException(Guid runId) : Exception($"Worker lost its lease for run {runId}; abandoning.");
    private sealed class SeapRunTransientException(string errorCode, string message) : Exception(message)
    {
        public string ErrorCode { get; } = errorCode;
    }

    [LoggerMessage(EventId = 5140, Level = LogLevel.Information, Message = "SEAP ingestion run {RunId} completed with status {Status}.")]
    private static partial void LogSeapRunCompleted(ILogger logger, Guid runId, SeapIngestionStatus status);
    [LoggerMessage(EventId = 5141, Level = LogLevel.Warning, Message = "SEAP ingestion run {RunId} scheduled a retry ({ErrorCode}) for {NextAttemptAt:O}.")]
    private static partial void LogSeapRunRetryScheduled(ILogger logger, Guid runId, string errorCode, DateTimeOffset nextAttemptAt);
    [LoggerMessage(EventId = 5142, Level = LogLevel.Error, Message = "SEAP ingestion run {RunId} could not schedule a retry.")]
    private static partial void LogSeapRunRetryFailed(ILogger logger, Guid runId, Exception exception);
    [LoggerMessage(EventId = 5145, Level = LogLevel.Warning, Message = "SEAP ingestion run {RunId} reached the maximum of {MaxAttempts} attempts and is marked Failed ({ErrorCode}).")]
    private static partial void LogSeapRunPermanentFailure(ILogger logger, Guid runId, string errorCode, int maxAttempts);
}
