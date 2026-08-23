using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.Seap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// HttpClient is always obtained from IHttpClientFactory; the client is never instantiated directly.
public sealed partial class SeapApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SeapOptions> options,
    ILogger<SeapApiClient> logger) : ISeapApiClient
{
    private readonly SeapOptions settings = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("seap");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 300));
        using HttpRequestMessage request = new(HttpMethod.Post, BuildUrl());
        request.Headers.Referrer = new Uri(new Uri(settings.BaseUrl), $"/pub/notices/contract-notices/list/{FirstNoticeTypeId()}/0");
        request.Content = JsonContent.Create(BuildSearchFilter(), options: JsonOptions);
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        SeapTenderNoticeDto[] notices = SeapNoticeParser.ParseNotices(body).ToArray();
        if (notices.Length == 0 && !string.IsNullOrWhiteSpace(body) && body.TrimStart().FirstOrDefault() is not '[' and not '{')
            LogSeapUnparseablePayload(logger);
        SeapTenderNoticeDto[] target = notices
            .Where(MatchesTargetCpv)
            .Take(Math.Max(0, settings.MaxNoticesPerPoll))
            .ToArray();
        LogSeapFetched(logger, notices.Length, target.Length);
        return target;
    }

    public async Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient("seap");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.RequestTimeoutSeconds, 5, 300));
        HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
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
}
