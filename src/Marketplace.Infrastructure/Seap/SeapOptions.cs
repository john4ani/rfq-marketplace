namespace Marketplace.Infrastructure.Seap;

public sealed class SeapOptions
{
    public const string SectionName = "Seap";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    /// <summary>Public SEAP notice-search endpoint (POST, JSON filter body).</summary>
    public string NoticesPath { get; init; } = "api-pub/NoticeCommon/GetCNoticeList/";
    /// <summary>Notice types to import/search. 2 = contract notice (CN); 17 = simplified-procedure notice
    /// (SCN) — the type where commune/town (e.g. "PRIMARIA COMUNEI POIANA, JUD. DB") tenders live.</summary>
    public int[] NoticeTypeIds { get; init; } = [2, 17];
    public int NoticesPageSize { get; init; } = 20;
    public int PollingIntervalSeconds { get; init; } = 3600;
    public string[] TargetCpvPrefixes { get; init; } = [];
    public int MaxNoticesPerPoll { get; init; } = 20;
    /// <summary>Hard cap on new tenders imported per rolling hour (cost guardrail).</summary>
    public int MaxNewNoticesPerHour { get; init; } = 5;
    /// <summary>On-demand „Caută în SEAP" search caps (per request): page size, max pages to scan,
    /// max matching tenders to import. The anonymous e-licitatie.ro search honors a large pageSize (500),
    /// so the whole capped slice (~3000 notices) is scanned with 6 requests. The max-matches cap is the
    /// breadth a single user-initiated search imports: the wide-window slice is chunked per month so the
    /// platform's ~3000-notice cap no longer hides a whole year, and documents are resolved on demand
    /// (POST /tenders/{id}/documents) so a bulk import stays cheap and can reach hundreds of matches.</summary>
    public int OnDemandSearchPageSize { get; init; } = 500;
    public int OnDemandSearchMaxPages { get; init; } = 6;
    public int OnDemandSearchMaxMatches { get; init; } = 500;
    public int OnDemandSearchMaxScanned { get; init; } = 3000;
    public int RequestTimeoutSeconds { get; init; } = 60;
    /// <summary>
    /// When the live e-licitatie.ro endpoint is unreachable, rate-limited, or returns a non-JSON
    /// payload, Development falls back to the golden payload files under <see cref="LocalFallbackDirectory"/>.
    /// </summary>
    public bool LocalFallbackEnabled { get; init; }
    /// <summary>Directory (relative to the API working directory) holding mock notice-search JSON files.</summary>
    public string LocalFallbackDirectory { get; init; } = "App_Data/SeapMocks";
    /// <summary>How often the ingestion-run worker ticks (seconds). Runs are only processed while
    /// <c>Features:SeapIngestionWorkerEnabled</c> is on.</summary>
    public int RunProcessingIntervalSeconds { get; init; } = 15;
    /// <summary>Lease length (seconds) a worker holds on a claimed run before it becomes reclaimable.</summary>
    public int RunLeaseSeconds { get; init; } = 120;
    /// <summary>How many active runs a single worker tick may claim/process.</summary>
    public int MaxRunsPerTick { get; init; } = 10;
    /// <summary>Global cap on simultaneously active resolve runs (cost guardrail before entitlement gating).</summary>
    public int MaxActiveResolveRuns { get; init; } = 25;
    /// <summary>Version stamped on every successful analysis; bump it to invalidate stored extractions.</summary>
    public int AnalysisVersion { get; init; } = 1;
    /// <summary>Descriptor of the analysis logic (prompt version | extraction schema version | model family /
    /// configuration). Included in <c>AnalysisRequestHash</c>, so a prompt/schema/model change produces a
    /// different request hash even when the notice and the document set are unchanged.</summary>
    public string AnalysisLogicDescriptor { get; init; } = "prompt-v1|schema-v1|default-model";
    /// <summary>Timeout (seconds) for a single live SEAP notice search/lookup call. On timeout the operation
    /// is routed to the normal retry/backoff path (never a suspended task).</summary>
    public int SearchTimeoutSeconds { get; init; } = 40;
    /// <summary>Timeout (seconds) for downloading one published document (applied per attachment inside the
    /// text extractor). On timeout the file is skipped/retried via the normal backoff.</summary>
    public int DocumentDownloadTimeoutSeconds { get; init; } = 60;
    /// <summary>Timeout (seconds) for the whole text-extraction pass over a tender's documents.</summary>
    public int TextExtractionTimeoutSeconds { get; init; } = 180;
    /// <summary>Timeout (seconds) for one LLM analysis call. On timeout the run stays active and retries
    /// (attempt-limited), never an unbounded suspended task.</summary>
    public int LlmTimeoutSeconds { get; init; } = 120;
    /// <summary>Base delay (seconds) for scheduling a retry of a transiently failed run (grows with attempts,
    /// jittered, capped by <see cref="MaxRetryDelayMinutes"/>).</summary>
    public int RunRetryBaseDelaySeconds { get; init; } = 30;
    /// <summary>Upper bound (minutes) for the retry backoff of a transiently failed run.</summary>
    public int MaxRetryDelayMinutes { get; init; } = 10;
    /// <summary>Maximum number of processing attempts (claims) allowed before a run is marked Failed and
    /// releases the global capacity slot. Retryable failures (LLM timeouts, SEAP hiccups) never become
    /// terminal Partial — they stay active until this limit.</summary>
    public int MaxIngestionAttempts { get; init; } = 5;
    /// <summary>How often (seconds) a processing worker renews its lease while a long operation (document
    /// download / text extraction / LLM call) is in flight. 0 disables heartbeat renewal.</summary>
    public int RunHeartbeatIntervalSeconds { get; init; } = 15;
}
