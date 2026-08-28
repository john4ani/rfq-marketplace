using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.Seap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// HttpClient is always obtained from IHttpClientFactory; the client is never instantiated directly.
// In Development, when the live endpoint is unreachable/rate-limited or returns a non-JSON payload,
// the fetch falls back to the golden payload files under Seap:LocalFallbackDirectory so parsing and
// the rest of the pipeline stay debuggable without network access.
public sealed partial class SeapApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SeapOptions> options,
    ILogger<SeapApiClient> logger) : ISeapApiClient
{
    private readonly SeapOptions settings = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken)
    {
        string? liveBody = null;
        string? liveError = null;
        try
        {
            HttpClient client = httpClientFactory.CreateClient("seap");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 300));
            using HttpRequestMessage request = new(HttpMethod.Post, BuildUrl());
            request.Headers.Referrer = new Uri(new Uri(settings.BaseUrl), $"/pub/notices/contract-notices/list/{FirstNoticeTypeId()}/0");
            request.Content = JsonContent.Create(BuildSearchFilter(), options: JsonOptions);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            liveBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception)
        {
            liveError = exception.Message;
        }

        // A parseable JSON envelope (even with zero notices) is the authoritative live answer.
        if (liveBody is not null && liveBody.TrimStart().FirstOrDefault() is '[' or '{')
        {
            SeapTenderNoticeDto[] notices = FilterTargetNotices(liveBody);
            LogSeapFetched(logger, notices.Length, notices.Length);
            return notices;
        }


        if (!settings.LocalFallbackEnabled)
        {
            if (liveError is null) LogSeapUnparseablePayload(logger);
            return [];
        }

        LogSeapLocalFallback(logger, liveError is not null ? liveError : "the live payload was not JSON");
        return await FetchFromLocalFallbackAsync(cancellationToken);
    }

    public async Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("seap");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 300));
        HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("seap");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 300));
            using HttpRequestMessage request = new(HttpMethod.Post, BuildUrl());
            request.Headers.Referrer = new Uri(new Uri(settings.BaseUrl), $"/pub/notices/contract-notices/list/{FirstNoticeTypeId()}/0");
            request.Content = JsonContent.Create(
                BuildPageSearchFilter(pageIndex, pageSize, startPublicationDate, endPublicationDate), options: JsonOptions);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();
            return SeapNoticeParser.ParseNotices(body);
        }
        catch (Exception exception)
        {
            LogSeapPageFetchFailed(logger, pageIndex, exception);
            return [];
        }
    }

    private SeapTenderNoticeDto[] FilterTargetNotices(string json)
    {
        SeapTenderNoticeDto[] notices = SeapNoticeParser.ParseNotices(json).ToArray();
        if (notices.Length == 0 && !string.IsNullOrWhiteSpace(json) && json.TrimStart().FirstOrDefault() is not '[' and not '{')
            LogSeapUnparseablePayload(logger);
        return notices.Where(MatchesTargetCpv).Take(Math.Max(0, settings.MaxNoticesPerPoll)).ToArray();
    }

    private async Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchFromLocalFallbackAsync(CancellationToken cancellationToken)
    {
        string directory = Path.GetFullPath(settings.LocalFallbackDirectory);
        string[] files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").OrderBy(file => file, StringComparer.Ordinal).ToArray()
            : [];
        if (files.Length == 0)
        {
            LogSeapFallbackEmpty(logger, directory);
            return [];
        }
        List<SeapTenderNoticeDto> merged = [];
        foreach (string file in files)
            merged.AddRange(FilterTargetNotices(await File.ReadAllTextAsync(file, cancellationToken)));
        LogSeapFallbackLoaded(logger, files.Length);
        return merged.Take(Math.Max(0, settings.MaxNoticesPerPoll)).ToArray();
    }

    private string BuildUrl() => new Uri(new Uri(settings.BaseUrl), settings.NoticesPath).ToString();

    private int FirstNoticeTypeId() => settings.NoticeTypeIds is { Length: > 0 } noticeTypes ? noticeTypes[0] : 2;

    private object BuildSearchFilter() => new
    {
        sysNoticeTypeIds = settings.NoticeTypeIds is { Length: > 0 } noticeTypes ? noticeTypes : new[] { 2 },
        sortProperties = Array.Empty<object>(),
        pageSize = Math.Max(1, settings.NoticesPageSize),
        pageIndex = 0
    };

    // The live e-licitatie.ro search honors a publication-date window (verified empirically: a
    // startPublicationDate of 2026-01-01 returns current-year notices instead of the stale unfiltered
    // slice). Keys are added only when set so "all history" behaves exactly like the plain search.
    private static Dictionary<string, object?> BuildPageSearchFilter(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate)
    {
        var filter = new Dictionary<string, object?>
        {
            ["sysNoticeTypeIds"] = new[] { 2 },
            ["sortProperties"] = Array.Empty<object>(),
            ["pageSize"] = Math.Max(1, pageSize),
            ["pageIndex"] = Math.Max(0, pageIndex)
        };
        if (startPublicationDate is not null)
            filter["startPublicationDate"] = startPublicationDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (endPublicationDate is not null)
            filter["endPublicationDate"] = endPublicationDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return filter;
    }

    private bool MatchesTargetCpv(SeapTenderNoticeDto notice)
    {
        if (settings.TargetCpvPrefixes.Length == 0) return true;
        return notice.CpvCodes.Any(code => settings.TargetCpvPrefixes.Any(prefix =>
            code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
    }

    [LoggerMessage(EventId = 5101, Level = LogLevel.Information, Message = "SEAP fetched {Total} notices; {Target} target the configured CPVs.")]
    private static partial void LogSeapFetched(ILogger logger, int total, int target);
    [LoggerMessage(EventId = 5102, Level = LogLevel.Warning, Message = "SEAP endpoint returned a non-JSON payload; no notices were parsed.")]
    private static partial void LogSeapUnparseablePayload(ILogger logger);
    [LoggerMessage(EventId = 5103, Level = LogLevel.Warning, Message = "SEAP live endpoint unavailable ({Reason}); using the local golden-payload fallback.")]
    private static partial void LogSeapLocalFallback(ILogger logger, string reason);
    [LoggerMessage(EventId = 5104, Level = LogLevel.Warning, Message = "SEAP local fallback directory {Directory} contains no JSON payloads.")]
    private static partial void LogSeapFallbackEmpty(ILogger logger, string directory);
    [LoggerMessage(EventId = 5105, Level = LogLevel.Information, Message = "SEAP local fallback loaded {FileCount} payload file(s).")]
    private static partial void LogSeapFallbackLoaded(ILogger logger, int fileCount);
    [LoggerMessage(EventId = 5110, Level = LogLevel.Warning, Message = "SEAP on-demand search page {PageIndex} failed; continuing.")]
    private static partial void LogSeapPageFetchFailed(ILogger logger, int pageIndex, Exception exception);
}
