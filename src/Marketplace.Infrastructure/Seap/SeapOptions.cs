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
}
