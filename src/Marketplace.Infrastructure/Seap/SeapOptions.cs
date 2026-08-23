namespace Marketplace.Infrastructure.Seap;

public sealed class SeapOptions
{
    public const string SectionName = "Seap";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    /// <summary>Public SEAP notice-search endpoint (POST, JSON filter body).</summary>
    public string NoticesPath { get; init; } = "api-pub/NoticeCommon/GetCNoticeList/";
    /// <summary>Notice types to import. 2 = contract notice (CN) on the public SEAP catalogue.</summary>
    public int[] NoticeTypeIds { get; init; } = [2];
    public int NoticesPageSize { get; init; } = 20;
    public int PollingIntervalSeconds { get; init; } = 3600;
    public string[] TargetCpvPrefixes { get; init; } = [];
    public int MaxNoticesPerPoll { get; init; } = 20;
    /// <summary>Hard cap on new tenders imported per rolling hour (cost guardrail).</summary>
    public int MaxNewNoticesPerHour { get; init; } = 5;
    public int RequestTimeoutSeconds { get; init; } = 60;
}
