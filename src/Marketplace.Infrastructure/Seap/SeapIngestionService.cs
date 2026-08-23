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
}
