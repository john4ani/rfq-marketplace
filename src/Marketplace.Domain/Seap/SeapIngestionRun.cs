namespace Marketplace.Domain.Seap;

/// <summary>
/// Why a SEAP tender entered the local cache. Drives what the tender may become: a <see cref="Standard"/>
/// background import and an <see cref="OnDemand"/> active lookup are ordinary bid targets, while
/// <see cref="DirectHistoricalLookup"/> is a deliberate research import of an already-expired notice — it
/// must never be surfaced as an active opportunity, enter current matching or feed a future auto-bid.
/// </summary>
public enum SeapImportMode
{
    Standard = 0,
    OnDemand = 1,
    DirectHistoricalLookup = 2
}

/// <summary>
/// Explicit lifecycle state of a SEAP ingestion run. <see cref="Pending"/> means the metadata was imported
/// and the run is waiting to be picked up; the terminal states are <see cref="Ready"/> (metadata + documents
/// + a document-grounded analysis), <see cref="Partial"/> (documents kept but the analysis failed or no
/// published documents exist) and <see cref="Failed"/> (metadata could not be made usable).
/// </summary>
public enum SeapIngestionStatus
{
    Pending = 0,
    FetchingDocuments = 1,
    Analyzing = 2,
    Ready = 3,
    Partial = 4,
    Failed = 5
}

/// <summary>
/// One idempotent, resumable „direct hit" ingestion attempt for a SEAP notice. A single active run per
/// tender is enforced (partial unique index on <c>TenderId</c> for the running statuses) so concurrent
/// resolves of the same notice reuse the existing run instead of duplicating document downloads/LLM cost.
/// The run is created by <c>POST /api/v1/seap/tenders/resolve</c> and processed by the background worker,
/// which claims the run with an expiring lease (<see cref="ClaimedBy"/>/<see cref="LeaseExpiresAt"/>/
/// <see cref="LastHeartbeatAt"/>). The persistent per-stage checkpoints (<see cref="DocumentsFetchedAt"/>,
/// <see cref="TextExtractedAt"/>) let the worker resume exactly where a previous attempt died (between
/// download, extraction and analysis) without re-downloading documents or re-running the LLM.
/// </summary>
public sealed class SeapIngestionRun
{
    private SeapIngestionRun() { }

    public Guid Id { get; private set; }
    public Guid TenderId { get; private set; }
    /// <summary>Human-readable notice number (e.g. SCN1179200) this run resolves.</summary>
    public string NoticeNo { get; private set; } = string.Empty;
    public SeapIngestionStatus Status { get; private set; }
    /// <summary>What this run must produce (see <see cref="SeapIngestionRunPurpose"/>). The worker executes
    /// the corresponding stage pipeline: <see cref="SeapIngestionRunPurpose.DocumentImport"/> durably stores
    /// the original published documents and reports Ready/Partial without text extraction or LLM analysis.</summary>
    public SeapIngestionRunPurpose Purpose { get; private set; } = SeapIngestionRunPurpose.DigestAndAnalyze;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    /// <summary>Id of the worker instance currently holding the run (for lease recovery).</summary>
    public string? ClaimedBy { get; private set; }
    /// <summary>Fencing token (monotonically increased on every claim). Every persisted checkpoint is written
    /// conditionally (<c>WHERE Id AND ClaimedBy AND ClaimVersion</c>); when zero rows are affected the worker
    /// lost its lease to another claimant and must abandon its result — this prevents a stale worker from
    /// overwriting the progress/final result of the worker that reclaimed the run.</summary>
    public long ClaimVersion { get; private set; }
    /// <summary>UTC instant when the current claim expires; an abandoned run is reclaimable afterwards.</summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    /// <summary>Earliest instant a retry may claim the run (transient failure backoff).</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }
    /// <summary>Checkpoint: set once the published documents were resolved and attached (idempotent download).
    /// Saved in the SAME transaction as the attachment metadata + <see cref="DocumentSetHash"/> stamp, so a
    /// non-null value always means the document metadata is durably persisted.</summary>
    public DateTimeOffset? DocumentsFetchedAt { get; private set; }
    /// <summary>Checkpoint: set once the readable text was extracted. Saved in the SAME transaction as
    /// <see cref="ExtractedText"/>, so a non-null value always means the text is persisted and reusable
    /// after a restart.</summary>
    public DateTimeOffset? TextExtractedAt { get; private set; }
    /// <summary>Extracted readable text of the published documents (persisted with the TextExtractedAt
    /// checkpoint so a resumed run never re-downloads/re-extracts nor analyzes without the text).</summary>
    public string? ExtractedText { get; private set; }
    /// <summary>Stable hash of the analysis request (notice + document set) — correlates attempts of the same
    /// logical analysis and, together with <see cref="AnalysisAttemptId"/>, documents the at-least-once
    /// execution guarantee for external LLM calls.</summary>
    public string? AnalysisRequestHash { get; private set; }
    /// <summary>Unique id issued for a single LLM analysis attempt; a new id is issued on every retry because
    /// a response lost to a crash cannot be recovered (external calls are at-least-once, persistence is
    /// idempotent, duplicates are minimized but not exactly-once).</summary>
    public string? AnalysisAttemptId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SeapIngestionRun Start(
        Guid tenderId, string noticeNo, DateTimeOffset now,
        SeapIngestionRunPurpose purpose = SeapIngestionRunPurpose.DigestAndAnalyze) => new()
    {
        Id = Guid.NewGuid(),
        TenderId = tenderId,
        NoticeNo = noticeNo,
        Purpose = purpose,
        Status = SeapIngestionStatus.Pending,
        StartedAt = now,
        AttemptCount = 0,
        CreatedAt = now,
        UpdatedAt = now
    };

    /// <summary>Atomically claims the run for a worker (the SQL claim uses the lease as the concurrency key;
    /// this only records the claim on the already-won entity). AttemptCount is the number of processing
    /// attempts and ClaimVersion the fencing token — both are bumped on every claim.</summary>
    public void Claim(string workerId, DateTimeOffset leaseExpiresAt, DateTimeOffset now)
    {
        ClaimedBy = workerId;
        ClaimVersion += 1;
        LeaseExpiresAt = leaseExpiresAt;
        LastHeartbeatAt = now;
        NextAttemptAt = null;
        AttemptCount += 1;
        UpdatedAt = now;
    }

    /// <summary>Opens a fresh LLM analysis attempt: issues a new attempt id (a response lost to a crash is
    /// not recoverable, so every retry is a new attempt) and stamps the deterministic request hash used to
    /// correlate attempts of the same logical analysis.</summary>
    public void BeginAnalysis(string requestHash, DateTimeOffset now)
    {
        AnalysisRequestHash = requestHash;
        AnalysisAttemptId = Guid.NewGuid().ToString("N");
        UpdatedAt = now;
    }

    public void Heartbeat(DateTimeOffset leaseExpiresAt, DateTimeOffset now)
    {
        LastHeartbeatAt = now;
        LeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = now;
    }

    /// <summary>Releases the claim after a transient failure while the run stays active for a later retry
    /// (the worker died between stages, SEAP hiccup, LLM timeout).</summary>
    public void ReleaseForRetry(string errorCode, string errorMessage, DateTimeOffset nextAttemptAt, DateTimeOffset now)
    {
        ClaimedBy = null;
        LeaseExpiresAt = null;
        NextAttemptAt = nextAttemptAt;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
        UpdatedAt = now;
    }

    public void SetStage(SeapIngestionStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    public void RecordDocumentsFetched(DateTimeOffset now)
    {
        DocumentsFetchedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Records the extraction checkpoint together with the extracted text (single transaction on the
    /// caller side): a non-null TextExtractedAt always means the text is persisted and reusable after restart.</summary>
    public void RecordTextExtracted(string? text, DateTimeOffset now)
    {
        ExtractedText = text;
        TextExtractedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Marks a terminal outcome (Ready/Partial/Failed) and releases the lease.</summary>
    public void Complete(SeapIngestionStatus status, DateTimeOffset now, string? errorCode = null, string? errorMessage = null)
    {
        Status = status;
        CompletedAt = now;
        ClaimedBy = null;
        LeaseExpiresAt = null;
        if (errorCode is not null)
        {
            LastErrorCode = errorCode;
            LastErrorMessage = errorMessage;
        }
        UpdatedAt = now;
    }
}
