using System.Text.Json;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

public sealed partial class SeapIngestionService(
    MarketplaceDbContext dbContext,
    ISeapApiClient apiClient,
    ISeapAttachmentStorage storage,
    ISeapCategorizer categorizer,
    IOptions<SeapOptions> options,
    TimeProvider timeProvider,
    ILogger<SeapIngestionService> logger) : ISeapIngestionService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SeapOptions settings = options.Value;

    public async Task<SeapTenderImportResult> ImportNoticeAsync(SeapTenderNoticeDto notice, CancellationToken cancellationToken)
    {
        if (await dbContext.SeapTenders.AsNoTracking()
                .AnyAsync(tender => tender.SeapExternalId == notice.Id, cancellationToken))
            return new(null, 0, true);

        DateTimeOffset now = timeProvider.GetUtcNow();
        SeapRawIngestion staging = SeapRawIngestion.Pending(notice.Id, JsonSerializer.Serialize(notice, PayloadJsonOptions), now);
        dbContext.SeapRawIngestions.Add(staging);

        SeapTender tender;
        try
        {
            tender = SeapTenderMapper.MapNotice(notice, now);
        }
        catch (Exception exception)
        {
            staging.MarkFailed(exception.Message, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            LogParseFailed(logger, notice.Id, exception);
            return new(null, 0, false);
        }

        dbContext.SeapTenders.Add(tender);

        // Enrichment (never a gate): county already came from the parser; the AI categorizer resolves the
        // CompaSpot category from the CPV label — first tender per CPV code pays one LLM call, the rest
        // hit the persistent cache. On any failure the tender still imports, uncategorized (the CPV-prefix
        // filter acts as fallback until the code is categorized).
        SeapCategorizationResult? categorization = await categorizer.CategorizeAsync(
            tender.CpvCodes.FirstOrDefault(), tender.CpvNames.FirstOrDefault(), tender.Title, cancellationToken);
        if (categorization is not null)
            tender.Categorize(categorization.CategoryId, now);

        int downloaded = 0;
        if (settings.Enabled)
        {
            foreach (SeapAttachmentDto attachment in notice.Attachments)
            {
                try
                {
                    using Stream content = await apiClient.DownloadAttachmentAsync(attachment.DownloadUrl, cancellationToken);
                    string s3Url = await storage.UploadAsync(tender.Id.ToString(), attachment.FileName, content, cancellationToken);
                    tender.AddAttachment(attachment.FileName, s3Url, now);
                    downloaded++;
                }
                catch (Exception exception)
                {
                    LogAttachmentFailed(logger, notice.Id, attachment.FileName, exception);
                }
            }
        }

        if (downloaded > 0 || notice.Attachments.Count == 0)
            tender.MarkReadyForBidding(now);

        staging.MarkParsed(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogSeapImported(logger, notice.Id, downloaded);
        return new(tender, downloaded, false);
    }

    public async Task<int> PollAndImportAsync(CancellationToken cancellationToken)
    {
        // Self-heal deterministic enrichment first: tenders imported before the county field existed get
        // their county re-derived from the contracting-authority name (idempotent, no AI).
        await BackfillCountiesAsync(cancellationToken);

        int maxNewPerHour = Math.Max(1, settings.MaxNewNoticesPerHour);
        if (await CountImportedThisHourAsync(cancellationToken) >= maxNewPerHour)
        {
            LogRateLimitReached(logger, maxNewPerHour);
            return 0;
        }

        IReadOnlyCollection<SeapTenderNoticeDto> notices = await apiClient.FetchRecentNoticesAsync(cancellationToken);
        int imported = 0;
        foreach (SeapTenderNoticeDto notice in notices)
        {
            if (imported >= maxNewPerHour) break;
            try
            {
                SeapTenderImportResult result = await ImportNoticeAsync(notice, cancellationToken);
                if (!result.Duplicate && result.Tender is not null) imported++;
            }
            catch (Exception exception)
            {
                LogImportFailed(logger, notice.Id, exception);
            }
        }
        return imported;
    }

    private Task<int> CountImportedThisHourAsync(CancellationToken cancellationToken) =>
        dbContext.SeapTenders.AsNoTracking()
            .CountAsync(tender => tender.CreatedAt >= timeProvider.GetUtcNow().AddHours(-1), cancellationToken);

    /// <summary>
    /// Deterministic, idempotent enrichment for tenders imported before the county field existed: re-derives
    /// <c>SeapTender.County</c> from the contracting-authority name via <see cref="CountyParser"/>. Never
    /// touches a tender that already has a county and never invents one (unrecognizable names stay null).
    /// </summary>
    public async Task<int> BackfillCountiesAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<SeapTender> missing = await dbContext.SeapTenders
            .Where(tender => tender.County == null)
            .ToListAsync(cancellationToken);
        int updated = 0;
        foreach (SeapTender tender in missing)
        {
            string? county = CountyParser.Parse(tender.BuyerName);
            if (county is null) continue;
            tender.BackfillCounty(county, now);
            updated++;
        }
        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            LogCountiesBackfilled(logger, updated);
        }
        return updated;
    }

    /// <summary>
    /// On-demand SEAP search: takes the active filter parameters, pages the live search (optionally within
    /// a publication-date window), derives each notice's county deterministically, imports the notices that
    /// match the given CPV prefixes/counties — without LLM categorization or attachment downloads — and
    /// returns them. User-initiated, so it bypasses the background hourly import cap.
    /// </summary>
    public async Task<IReadOnlyCollection<SeapTender>> SearchAndImportAsync(
        string[]? cpvPrefixes, string[]? counties, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        string[] canonicalCounties = (counties ?? [])
            .Select(RomanianCountyCodes.TryNormalize)
            .Where(county => county is not null).Select(county => county!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] cpvPrefixes2 = (cpvPrefixes ?? [])
            .Select(prefix => CpvPrefix.ToMatchPattern(prefix).TrimEnd('%'))
            .Where(prefix => prefix.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        int pageSize = Math.Max(50, settings.OnDemandSearchPageSize);
        int maxMatches = Math.Max(1, settings.OnDemandSearchMaxMatches);
        int maxScanned = Math.Max(pageSize, settings.OnDemandSearchMaxScanned);
        DateTimeOffset now = timeProvider.GetUtcNow();
        var imported = new List<SeapTender>();
        int scanned = 0;
        for (int pageIndex = 0; pageIndex < Math.Max(1, settings.OnDemandSearchMaxPages); pageIndex++)
        {
            IReadOnlyCollection<SeapTenderNoticeDto> notices = await apiClient.FetchNoticePageAsync(
                pageIndex, pageSize, fromDate, toDate, cancellationToken);
            if (notices.Count == 0) break;
            foreach (SeapTenderNoticeDto notice in notices)
            {
                scanned++;
                if (canonicalCounties.Length > 0 && (notice.County is null || !canonicalCounties.Contains(notice.County)))
                    continue;
                if (cpvPrefixes2.Length > 0 && !notice.CpvCodes.Any(code =>
                        cpvPrefixes2.Any(prefix => code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                    continue;
                SeapTender? tender = await ImportSearchNoticeAsync(notice, now, cancellationToken);
                if (tender is not null)
                {
                    imported.Add(tender);
                    if (imported.Count >= maxMatches) break;
                }
            }
            if (imported.Count >= maxMatches || scanned >= maxScanned) break;
        }
        if (imported.Count > 0)
            LogSeapSearchImported(logger, imported.Count, scanned);
        return imported;
    }

    /// <summary>Persists a search-found notice without LLM categorization and without attachment downloads:
    /// the on-demand search is about discovering matching tenders, and the deterministic county + CPV codes
    /// are enough to drive the list filters. Duplicates are skipped (the notice is already in the DB).</summary>
    private async Task<SeapTender?> ImportSearchNoticeAsync(
        SeapTenderNoticeDto notice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await dbContext.SeapTenders.AsNoTracking()
                .AnyAsync(tender => tender.SeapExternalId == notice.Id, cancellationToken))
            return null;
        SeapRawIngestion staging = SeapRawIngestion.Pending(notice.Id, JsonSerializer.Serialize(notice, PayloadJsonOptions), now);
        SeapTender tender;
        try
        {
            tender = SeapTenderMapper.MapNotice(notice, now);
        }
        catch (Exception exception)
        {
            staging.MarkFailed(exception.Message, now);
            dbContext.SeapRawIngestions.Add(staging);
            await dbContext.SaveChangesAsync(cancellationToken);
            LogParseFailed(logger, notice.Id, exception);
            return null;
        }
        if (notice.Attachments.Count == 0)
            tender.MarkReadyForBidding(now);
        staging.MarkParsed(now);
        dbContext.SeapRawIngestions.Add(staging);
        dbContext.SeapTenders.Add(tender);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tender;
    }

    [LoggerMessage(EventId = 5103, Level = LogLevel.Information, Message = "SEAP tender {SeapId} imported with {AttachmentCount} attachments.")]
    private static partial void LogSeapImported(ILogger logger, string seapId, int attachmentCount);
    [LoggerMessage(EventId = 5104, Level = LogLevel.Warning, Message = "SEAP attachment {FileName} for {SeapId} failed; continuing.")]
    private static partial void LogAttachmentFailed(ILogger logger, string seapId, string fileName, Exception exception);
    [LoggerMessage(EventId = 5105, Level = LogLevel.Warning, Message = "SEAP tender {SeapId} import failed.")]
    private static partial void LogImportFailed(ILogger logger, string seapId, Exception exception);
    [LoggerMessage(EventId = 5106, Level = LogLevel.Warning, Message = "SEAP hourly import cap ({Cap} new tenders) already reached; skipping this poll.")]
    private static partial void LogRateLimitReached(ILogger logger, int cap);
    [LoggerMessage(EventId = 5107, Level = LogLevel.Warning, Message = "SEAP tender {SeapId} could not be parsed; recorded as Failed in SeapRawIngestion.")]
    private static partial void LogParseFailed(ILogger logger, string seapId, Exception exception);
    [LoggerMessage(EventId = 5109, Level = LogLevel.Information, Message = "Backfilled county for {Count} previously imported SEAP tenders.")]
    private static partial void LogCountiesBackfilled(ILogger logger, int count);
    [LoggerMessage(EventId = 5111, Level = LogLevel.Information, Message = "SEAP on-demand search imported {Imported} matching tenders after scanning {Scanned} notices.")]
    private static partial void LogSeapSearchImported(ILogger logger, int imported, int scanned);
}
