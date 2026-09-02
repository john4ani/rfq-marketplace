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
    ISeapSummaryGenerator summaryGenerator,
    ISeapAttachmentTextReader attachmentTextReader,
    IOptions<SeapOptions> options,
    TimeProvider timeProvider,
    ILogger<SeapIngestionService> logger) : ISeapIngestionService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SeapIngestionStatus[] ActiveIngestionStatuses =
    [
        SeapIngestionStatus.Pending,
        SeapIngestionStatus.FetchingDocuments,
        SeapIngestionStatus.Analyzing
    ];
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

        // AI detail extraction („Sinteză AI" bullets + award criterion/guarantee/funding/duration/qualification),
        // fed with the notice data the importer holds (attachment text is not extracted at SEAP ingestion yet,
        // so it is passed as null). Enrichment is never a gate: a failed call leaves the tender without AI
        // details and is persisted in the same SaveChanges as the tender.
        SeapAiExtraction? extraction = await summaryGenerator.GenerateAsync(tender, attachmentText: null, cancellationToken);
        if (extraction is not null)
            tender.ApplyAiExtraction(extraction, now);

        // Published documents (Fișa de date, Caiet de sarcini, formulare) resolved from the live notice-detail
        // API; stored as remote references streamed on demand by the download endpoint. Never a gate.
        await FetchAndAttachDocumentsAsync(notice, tender, now, cancellationToken);

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
            string? county = CountyParser.Parse(tender.BuyerName)
                ?? CountyParser.Parse($"{tender.BuyerName} {tender.Title}");
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
        int scanned;
        if (fromDate is null && toDate is null)
        {
            // „Tot istoricul" without a window keeps the single unfiltered scan (the platform's wide
            // unfiltered slice is stale anyway) + the recent complement below.
            scanned = await ScanAndImportAsync(cpvPrefixes2, canonicalCounties, fromDate, toDate,
                pageSize, maxMatches, maxScanned, imported, now, cancellationToken);
        }
        else
        {
            // The anonymous e-licitatie.ro search caps a wide publication window at ~3000 notices and
            // returns an unstable slice (verified live), so a whole-year window hides most of the corpus —
            // e.g. a Suceava search saw ~13 tenders while SEAP itself shows ~1000 for the current year.
            // Chunk the requested window per calendar month: each month fits under the platform cap, so
            // every month's slice is complete and the county/CPV filters see the real universe. The global
            // match cap (OnDemandSearchMaxMatches) still bounds total imports per search.
            DateOnly from = fromDate ?? DateOnly.FromDateTime(now.UtcDateTime.AddYears(-1));
            DateOnly to = toDate ?? DateOnly.FromDateTime(now.UtcDateTime);
            scanned = 0;
            foreach ((DateOnly chunkFrom, DateOnly chunkTo) in ChunkByMonth(from, to))
            {
                if (imported.Count >= maxMatches) break;
                scanned += await ScanAndImportAsync(cpvPrefixes2, canonicalCounties, chunkFrom, chunkTo,
                    pageSize, maxMatches, maxScanned, imported, now, cancellationToken);
            }
        }

        // The trailing 30 days — clamped to the requested window — stays as an uncapped complement for the
        // unfiltered ("all history") path and as a safety net if a single month still exceeds the platform
        // slice; the importer skips duplicates, so merged results stay unique.
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        DateOnly recentFrom = today.AddDays(-30);
        if (fromDate is { } requestedFrom && requestedFrom > recentFrom) recentFrom = requestedFrom;
        DateOnly recentTo = toDate is { } requestedTo && requestedTo < today ? requestedTo : today;
        if (recentFrom <= recentTo && imported.Count < maxMatches)
            scanned += await ScanAndImportAsync(cpvPrefixes2, canonicalCounties, recentFrom, recentTo,
                pageSize, maxMatches, maxScanned, imported, now, cancellationToken);

        if (imported.Count > 0)
            LogSeapSearchImported(logger, imported.Count, scanned);
        return imported;
    }

    /// <summary>Splits a publication-date window into calendar months (the first/last chunk clamp to the
    /// requested bounds). Each month's slice stays below the platform's ~3000-notice cap, so chunking makes
    /// a wide window's coverage complete instead of an unstable top slice.</summary>
    private static IEnumerable<(DateOnly From, DateOnly To)> ChunkByMonth(DateOnly from, DateOnly to)
    {
        int year = from.Year;
        int month = from.Month;
        while (new DateOnly(year, month, 1) <= to)
        {
            DateOnly chunkFrom = new DateOnly(year, month, 1);
            if (chunkFrom < from) chunkFrom = from;
            DateOnly chunkTo = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            if (chunkTo > to) chunkTo = to;
            yield return (chunkFrom, chunkTo);
            if (month == 12)
            {
                year++;
                month = 1;
            }
            else
            {
                month++;
            }
        }
    }

    /// <summary>Pages the live SEAP search for one publication-date window, importing the notices that match
    /// the given CPV prefixes/counties, and returns how many notices were scanned. Each window gets its own
    /// scan budget (<paramref name="maxScanned"/>) so a second (complement) window is never starved by a
    /// capped first window.</summary>
    private async Task<int> ScanAndImportAsync(
        string[] cpvPrefixes, string[] counties, DateOnly? fromDate, DateOnly? toDate,
        int pageSize, int maxMatches, int maxScanned, List<SeapTender> imported,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        int scanned = 0;
        for (int pageIndex = 0; pageIndex < Math.Max(1, settings.OnDemandSearchMaxPages); pageIndex++)
        {
            if (imported.Count >= maxMatches || scanned >= maxScanned) break;
            IReadOnlyCollection<SeapTenderNoticeDto> notices = await apiClient.FetchNoticePageAsync(
                pageIndex, pageSize, fromDate, toDate, cancellationToken);
            if (notices.Count == 0) break;
            foreach (SeapTenderNoticeDto notice in notices)
            {
                if (imported.Count >= maxMatches || scanned >= maxScanned) break;
                scanned++;
                if (counties.Length > 0 && (notice.County is null || !counties.Contains(notice.County)))
                    continue;
                if (cpvPrefixes.Length > 0 && !notice.CpvCodes.Any(code =>
                        cpvPrefixes.Any(prefix => code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                    continue;
                SeapTender? tender = await ImportSearchNoticeAsync(notice, now, cancellationToken);
                if (tender is not null)
                {
                    imported.Add(tender);
                    if (imported.Count >= maxMatches) break;
                }
            }
        }
        return scanned;
    }

    /// <summary>Persists a search-found notice without LLM categorization, without attachment resolution and
    /// without the summarizer: the on-demand search is about discovering matching tenders fast and in volume
    /// (up to <c>OnDemandSearchMaxMatches</c> per request), and the deterministic county + CPV codes are
    /// enough to drive the list filters. Duplicates are skipped (the notice is already in the DB).</summary>
    private async Task<SeapTender?> ImportSearchNoticeAsync(
        SeapTenderNoticeDto notice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        SeapTender? existing = await dbContext.SeapTenders
            .SingleOrDefaultAsync(tender => tender.SeapExternalId == notice.Id, cancellationToken);
        if (existing is not null)
        {
            // The search doubles as a light self-heal: a tender imported before the numeric notice id existed
            // gets it backfilled so its documents can be resolved on demand later (POST /tenders/{id}/documents
            // resolves them — the same flow the „Documente Originale" tab fires). No document fetch here: a
            // bulk search must not pay one notice-detail HTTP call per already-known tender.
            if (existing.NoticeId is null && notice.NoticeId is int noticeId)
            {
                existing.BackfillNoticeSource(noticeId, notice.SysNoticeTypeId, now);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return null;
        }
        // Expired notices are never bid targets — the on-demand search must not import (or clutter the
        // local cache with) tenders whose submission deadline already passed.
        if (notice.Deadline < now)
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
        // No synchronous LLM call and no per-notice document-resolution call here: the AI detail extraction
        // („Sinteză AI" bullets + award criterion / guarantee / funding / duration / qualification) is
        // deferred to the idempotent POST /tenders/{id}/summary, and the published documents resolve on
        // demand via POST /tenders/{id}/documents when the „Documente Originale" tab opens. A live search
        // can match up to OnDemandSearchMaxMatches notices, and one LLM + one notice-detail HTTP call per
        // notice inside the request thread is what made „Caută în SEAP" both slow and too narrow. The detail
        // page already renders the honest skeleton / „Date indisponibile în anunț" states until then.
        if (notice.Attachments.Count == 0)
            tender.MarkReadyForBidding(now);
        staging.MarkParsed(now);
        dbContext.SeapRawIngestions.Add(staging);
        dbContext.SeapTenders.Add(tender);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tender;
    }

    /// <summary>Resolves the published documents of a notice (Fișa de date, Caiet de sarcini, formulare)
    /// from the live notice-detail API and attaches them to the tender as remote references — the file
    /// content streams from e-licitatie.ro on demand through our download endpoint, so the import stays
    /// cheap. Enrichment, never a gate: any failure leaves the tender without attachments. Returns how many
    /// documents were attached.</summary>
    private async Task<int> FetchAndAttachDocumentsAsync(
        SeapTenderNoticeDto notice, SeapTender tender, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (notice.NoticeId is not int noticeId) return 0;
        try
        {
            IReadOnlyCollection<SeapNoticeDocumentDto> documents =
                await apiClient.FetchNoticeDocumentsAsync(noticeId, notice.SysNoticeTypeId ?? 2, cancellationToken);
            int attached = 0;
            foreach (SeapNoticeDocumentDto document in documents)
            {
                if (tender.Attachments.Any(attachment =>
                        string.Equals(attachment.FileName, document.FileName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                tender.AddAttachment(document.FileName, document.DownloadUrl, now);
                attached++;
            }
            return attached;
        }
        catch (Exception exception)
        {
            LogDocumentsAttachFailed(logger, notice.Id, exception);
            return 0;
        }
    }

    /// <summary>
    /// „Direct hit" resolve by SEAP notice number (SCN…/CN…). Local first: a tender that already carries a
    /// document-grounded analysis is returned as ready (no network call, no run). A local row that is not
    /// ready yet gets an active ingestion run — never a re-search, the row's metadata/NoticeId are kept.
    /// Only on a genuine local miss is the live platform queried with an exact notice-number match; when the
    /// notice exists it is upserted by the unique normalized id (metadata + NoticeId) and an ingestion run is
    /// started. Expired notices are imported as <see cref="SeapImportMode.DirectHistoricalLookup"/> — they are
    /// never promoted to the active feed. This call deliberately performs NO document download and NO LLM
    /// analysis: those belong to the background ingestion worker and are tracked on the returned run.
    /// Returns null when SEAP answered normally and has no such notice; propagates
    /// <see cref="SeapSourceUnavailableException"/> when the platform is unreachable/erroring.
    /// </summary>
    public async Task<SeapResolveResult?> ResolveNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken)
    {
        if (!SeapNoticeNumber.TryNormalize(noticeNo, out string normalized))
            return null;
        DateTimeOffset now = timeProvider.GetUtcNow();

        SeapTender? tender = await dbContext.SeapTenders
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.NormalizedExternalId == normalized, cancellationToken);
        if (tender is not null)
            return await ResolveLocalTenderAsync(tender, normalized, now, cancellationToken);

        SeapTenderNoticeDto? notice = await apiClient.FetchNoticeByNumberAsync(normalized, cancellationToken);
        return notice is null ? null : await UpsertFromLiveAsync(notice, normalized, now, cancellationToken);
    }

    /// <summary>Current ingestion state of a tender: the persisted run status (or <see cref="SeapIngestionStatus.Pending"/>
    /// when no run exists yet) plus the tender-level import markers that let the worker skip unchanged work.</summary>
    public async Task<SeapIngestionStatusDto?> GetIngestionStatusAsync(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await dbContext.SeapTenders.AsNoTracking()
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken);
        if (tender is null) return null;

        SeapIngestionRun? run = await dbContext.SeapIngestionRuns.AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(item => item.TenderId == tenderId, cancellationToken);
        bool hasActiveRun = await dbContext.SeapIngestionRuns.AsNoTracking()
            .AnyAsync(item => item.TenderId == tenderId && ActiveIngestionStatuses.Contains(item.Status), cancellationToken);
        string status = SeapTenderReadiness.IsDocumentAnalysisReady(tender) && !hasActiveRun
            ? SeapIngestionStatus.Ready.ToString()
            : hasActiveRun
                ? (await dbContext.SeapIngestionRuns.AsNoTracking()
                    .FirstAsync(item => item.TenderId == tenderId && ActiveIngestionStatuses.Contains(item.Status), cancellationToken)).Status.ToString()
                : run is null
                    ? SeapIngestionStatus.Pending.ToString()
                    // A previously Ready analysis whose document set changed is stale — it must be re-run.
                    : run.Status == SeapIngestionStatus.Ready
                        ? SeapIngestionStatus.Pending.ToString()
                        : run.Status.ToString();
        return new SeapIngestionStatusDto(
            tender.Id, tender.SeapExternalId, status, tender.ImportMode.ToString(),
            run?.Id, tender.AttemptCount, tender.StartedAt, tender.CompletedAt,
            tender.LastErrorCode, tender.LastErrorMessage, tender.DocumentSetHash, tender.AnalysisVersion,
            tender.DocumentDigestStatus.ToString(), run?.DocumentsFetchedAt, run?.TextExtractedAt);
    }

    /// <summary>Resolve outcome for a row already in the local cache, using the single centralized readiness
    /// rule: only a document-grounded analysis of the CURRENT document set with no active rebuild run is
    /// served as ready (200). Everything else bumps the attempt marker and ensures one active ingestion run —
    /// never a re-search of SEAP when the tender already exists.</summary>
    private async Task<SeapResolveResult> ResolveLocalTenderAsync(
        SeapTender tender, string normalized, DateTimeOffset now, CancellationToken cancellationToken)
    {
        bool hasActiveRun = await dbContext.SeapIngestionRuns.AsNoTracking()
            .AnyAsync(run => run.TenderId == tender.Id && ActiveIngestionStatuses.Contains(run.Status), cancellationToken);
        if (!hasActiveRun && SeapTenderReadiness.IsDocumentAnalysisReady(tender))
            return new(tender.Id, normalized, SeapIngestionStatus.Ready.ToString(), null, SeapTenderDetailsDto.From(tender, now));

        // A tender whose best possible result is a metadata-only analysis (no published/lisible documents)
        // terminates as Partial — nothing more can be produced, so do not spin a new run on every resolve.
        if (!hasActiveRun)
        {
            SeapIngestionRun? latest = await dbContext.SeapIngestionRuns.AsNoTracking()
                .OrderByDescending(run => run.UpdatedAt)
                .FirstOrDefaultAsync(run => run.TenderId == tender.Id, cancellationToken);
            if (latest is not null && latest.Status == SeapIngestionStatus.Partial
                && (latest.LastErrorCode == SeapIngestionService.ErrorNoPublishedDocuments
                    || latest.LastErrorCode == SeapIngestionService.ErrorNoReadableDocuments)
                && tender.SummaryPoints.Count > 0)
            {
                return new(tender.Id, normalized, SeapIngestionStatus.Partial.ToString(), latest.Id, SeapTenderDetailsDto.From(tender, now));
            }
        }

        SeapImportMode importMode = tender.Deadline < now ? SeapImportMode.DirectHistoricalLookup : SeapImportMode.OnDemand;
        tender.RecordResolveAttempt(importMode, now);
        SeapIngestionRun run = await EnsureActiveRunAsync(tender.Id, normalized, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(tender.Id, normalized, run.Status.ToString(), run.Id, null);
    }

    /// <summary>Returns the single active run for a tender (or creates one). The partial unique index on the
    /// running statuses makes the create idempotent under concurrency: a lost race reloads and returns the
    /// run the other request created.</summary>
    private async Task<SeapIngestionRun> EnsureActiveRunAsync(
        Guid tenderId, string normalized, DateTimeOffset now, CancellationToken cancellationToken)
    {
        SeapIngestionRun? active = await dbContext.SeapIngestionRuns
            .FirstOrDefaultAsync(run => run.TenderId == tenderId && ActiveIngestionStatuses.Contains(run.Status), cancellationToken);
        if (active is not null) return active;

        // Cost guardrail: never let a user (or a flood of resolve calls) queue unbounded LLM work. The
        // count-then-insert check is an operational SOFT cap (two concurrent requests can slip past it by a
        // few runs); exact enforcement would need a distributed lock/advisory lock — the cap exists to bound
        // worst-case cost, not to guarantee a precise concurrency count.
        int activeRuns = await dbContext.SeapIngestionRuns.AsNoTracking()
            .CountAsync(run => ActiveIngestionStatuses.Contains(run.Status), cancellationToken);
        if (activeRuns >= Math.Max(1, settings.MaxActiveResolveRuns))
            throw new SeapResolveLimitReachedException(
                $"Limita de procesări SEAP concurente a fost atinsă ({settings.MaxActiveResolveRuns}). Încearcă din nou mai târziu.");

        SeapIngestionRun run = SeapIngestionRun.Start(tenderId, normalized, now);
        dbContext.SeapIngestionRuns.Add(run);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(run).State = EntityState.Detached;
            SeapIngestionRun? concurrent = await dbContext.SeapIngestionRuns.AsNoTracking()
                .FirstOrDefaultAsync(other => other.TenderId == tenderId && ActiveIngestionStatuses.Contains(other.Status), cancellationToken);
            if (concurrent is not null) return concurrent;
            throw;
        }
        return run;
    }

    /// <summary>Idempotent upsert of a notice returned by the live exact-match lookup: maps and persists the
    /// metadata + numeric NoticeId (unique <see cref="SeapTender.NormalizedExternalId"/> makes the insert
    /// race-safe), starts one ingestion run and stages the raw payload. Expired notices become
    /// <see cref="SeapImportMode.DirectHistoricalLookup"/> and stay <see cref="SeapTenderStatus.Imported"/> —
    /// no premature ReadyForBidding and never an active-opportunity surface.</summary>
    private async Task<SeapResolveResult?> UpsertFromLiveAsync(
        SeapTenderNoticeDto notice, string normalized, DateTimeOffset now, CancellationToken cancellationToken)
    {
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

        dbContext.SeapTenders.Add(tender);
        SeapImportMode importMode = notice.Deadline < now ? SeapImportMode.DirectHistoricalLookup : SeapImportMode.OnDemand;
        tender.RecordResolveAttempt(importMode, now);
        SeapIngestionRun run = SeapIngestionRun.Start(tender.Id, normalized, now);
        dbContext.SeapIngestionRuns.Add(run);
        staging.MarkParsed(now);
        dbContext.SeapRawIngestions.Add(staging);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request won the normalized-external-id upsert race — reuse its row instead of failing.
            dbContext.Entry(tender).State = EntityState.Detached;
            dbContext.Entry(run).State = EntityState.Detached;
            dbContext.Entry(staging).State = EntityState.Detached;
            SeapTender? existing = await dbContext.SeapTenders.AsNoTracking()
                .Include(item => item.Attachments)
                .SingleOrDefaultAsync(item => item.NormalizedExternalId == normalized, cancellationToken);
            if (existing is null) throw;
            return await ResolveLocalTenderAsync(existing, normalized, now, cancellationToken);
        }
        return new(tender.Id, normalized, run.Status.ToString(), run.Id, null);
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
    [LoggerMessage(EventId = 5112, Level = LogLevel.Warning, Message = "SEAP tender {SeapId} published documents could not be attached; tender stays without attachments.")]
    private static partial void LogDocumentsAttachFailed(ILogger logger, string seapId, Exception exception);
}
