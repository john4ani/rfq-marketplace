using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Livrarea 2 — worker semantics on the shared PostgreSQL test container:
//   • one resolve → one run, advanced to Ready by a worker tick (documents attached, hash stamped, grounded
//     analysis recorded, active tender promoted to ReadyForBidding);
//   • a DirectHistoricalLookup run reaches Ready but is never promoted to an active bid target;
//   • an active run with an unexpired lease cannot be claimed twice; after the lease expires it is resumed;
//   • a Failed run allows a fresh run;
//   • changing the document set invalidates the stored analysis (no stale 200; a new run is started).
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapIngestionRunWorkerTests : IntegrationTestBase
{
    // The worker tests exercise the real claim/lease pipeline against the shared test database, so any run
    // left active by a failed run of this class would be claimed by the next test. Purging exactly the notice
    // numbers owned by this class before every test keeps the suite deterministic without touching data of
    // the other SEAP suites.
    private static readonly string[] ClassNoticeNumbers =
    [
        "SCN500001", "SCN500002", "SCN500003", "SCN500004", "SCN500005",
        "SCN510001", "SCN510002", "SCN510003", "SCN510004", "SCN510005", "SCN510006",
        "SCN520001", "SCN520002", "SCN520003", "SCN520004"
    ];

    public SeapIngestionRunWorkerTests()
    {
        using MarketplaceDbContext cleanup = new(TestDatabase.CreateOptions());
        cleanup.SeapIngestionRuns
            .Where(run => ClassNoticeNumbers.Contains(run.NoticeNo)).ExecuteDelete();
        cleanup.SeapTenders
            .Where(tender => tender.NormalizedExternalId != null && ClassNoticeNumbers.Contains(tender.NormalizedExternalId)).ExecuteDelete();
    }

    [Fact]
    public async Task WorkerAdvancesARunToReadyWithGroundedAnalysis()
    {
        await using MarketplaceDbContext db = CreateDatabase();

        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500001", "Reparații la școală", null, ["45214100-1"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
            [], County: "Dâmbovița", NoticeId: 900_501, SysNoticeTypeId: 17));
        WorkerSummaryGenerator summary = new();
        SeapIngestionService service = CreateService(db, api, summary, new WorkerTextReader("FISA DE DATE — lucrări de reparații\nGaranție de participare: 120.000 RON"));

        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN500001", CancellationToken.None);
        Assert.NotNull(resolved);
        Assert.Equal("Pending", resolved.Status);

        int processed = await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
        Assert.Equal(1, processed);

        SeapTender saved = await db.SeapTenders
            .Include(tender => tender.Attachments)
            .SingleAsync(tender => tender.NormalizedExternalId == "SCN500001");
        Assert.Single(saved.Attachments);
        Assert.False(string.IsNullOrWhiteSpace(saved.DocumentSetHash));
        Assert.Equal(saved.DocumentSetHash, saved.AnalyzedDocumentSetHash);
        Assert.True(saved.SummaryGroundedInDocuments);
        Assert.Equal(1, saved.AnalysisVersion);
        // Active (non-historical) tender becomes a bid target only after the grounded analysis exists.
        Assert.Equal(SeapTenderStatus.ReadyForBidding, saved.Status);

        SeapIngestionRun run = await db.SeapIngestionRuns.SingleAsync(item => item.TenderId == saved.Id);
        Assert.Equal(SeapIngestionStatus.Ready, run.Status);
        Assert.NotNull(run.DocumentsFetchedAt);
        Assert.NotNull(run.TextExtractedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.Null(run.ClaimedBy);
        Assert.Null(run.LeaseExpiresAt);
        // The document text reached the summarizer.
        Assert.Contains("Garanție de participare: 120.000 RON", summary.LastAttachmentText ?? string.Empty);
    }

    [Fact]
    public async Task HistoricalRunReachesReadyButIsNeverPromotedToABidTarget()
    {
        await using MarketplaceDbContext db = CreateDatabase();

        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500002", "Reparații la școală (istoric)", null, ["45214100-1"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-5),
            [], County: "Dâmbovița", NoticeId: 900_502, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        await service.ResolveNoticeByNumberAsync("SCN500002", CancellationToken.None);
        int processed = await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
        Assert.Equal(1, processed);

        SeapTender saved = await db.SeapTenders.SingleAsync(tender => tender.NormalizedExternalId == "SCN500002");
        Assert.Equal(SeapImportMode.DirectHistoricalLookup, saved.ImportMode);
        Assert.True(saved.SummaryGroundedInDocuments);
        // Research history: even with the analysis completed it is NOT an active bid target.
        Assert.Equal(SeapTenderStatus.Imported, saved.Status);
    }

    [Fact]
    public async Task ActiveRunWithUnexpiredLeaseCannotBeClaimedButIsResumedAfterExpiry()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500003", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_503, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN500003", CancellationToken.None);
        Guid runId = resolved!.RunId!.Value;

        // Simulate a worker that claimed the run and then died mid-processing (lease still valid).
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapIngestionRun active = await db.SeapIngestionRuns.SingleAsync(run => run.Id == runId);
        active.Claim("dead-worker", now.AddMinutes(2), now);
        active.SetStage(SeapIngestionStatus.FetchingDocuments, now);
        await db.SaveChangesAsync();

        // A second worker cannot claim it while the lease is unexpired.
        Assert.Equal(0, await service.ProcessPendingIngestionRunsAsync("worker-2", 5, CancellationToken.None));

        // Once the lease expires the run is reclaimed and resumed from its checkpoints (fresh worker scope,
        // as in production — the claim is a DB-side atomic UPDATE on a context that has no cached entity).
        SeapIngestionRun expired = await db.SeapIngestionRuns.SingleAsync(run => run.Id == runId);
        expired.ReleaseForRetry("LeaseExpired", "run abandoned", now.AddMinutes(-1), now);
        expired.SetStage(SeapIngestionStatus.Pending, now);
        await db.SaveChangesAsync();

        await using MarketplaceDbContext workerDb = CreateDatabase();
        SeapIngestionService worker2 = CreateService(workerDb, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));
        int processed = await worker2.ProcessPendingIngestionRunsAsync("worker-2", 5, CancellationToken.None);
        Assert.Equal(1, processed);
        SeapIngestionRun completed = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.Id == runId);
        Assert.Equal(SeapIngestionStatus.Ready, completed.Status);
        Assert.Equal(2, completed.AttemptCount); // dead-worker claim + worker-2 recovery claim
    }

    [Fact]
    public async Task FailedRunAllowsAFreshRun()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500004", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_504, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult? first = await service.ResolveNoticeByNumberAsync("SCN500004", CancellationToken.None);
        Guid tenderId = first!.TenderId;
        SeapIngestionRun failed = await db.SeapIngestionRuns.SingleAsync(run => run.TenderId == tenderId);
        failed.Complete(SeapIngestionStatus.Failed, DateTimeOffset.UtcNow, "PermanentError", "ceva permanent");
        await db.SaveChangesAsync();

        SeapResolveResult? second = await service.ResolveNoticeByNumberAsync("SCN500004", CancellationToken.None);
        Assert.NotNull(second);
        Assert.Equal("Pending", second.Status);
        Assert.NotNull(second.RunId);
        Assert.Equal(2, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == tenderId));
    }

    [Fact]
    public async Task ChangedDocumentSetInvalidatesTheStoredAnalysisAndTriggersARerun()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500005", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_505, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN500005", CancellationToken.None);
        await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
        SeapTender ready = await db.SeapTenders.SingleAsync(tender => tender.Id == resolved!.TenderId);
        Assert.True(SeapTenderReadiness.IsDocumentAnalysisReady(ready));

        // The authority publishes a changed document set → the stored analysis is invalidated.
        ready.RecordDocumentSetHash("changed-document-hash", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        Assert.False(SeapTenderReadiness.IsDocumentAnalysisReady(ready));

        // A resolve of the same notice must NOT return 200 with the stale analysis — it starts a new run.
        SeapResolveResult? again = await service.ResolveNoticeByNumberAsync("SCN500005", CancellationToken.None);
        Assert.NotNull(again);
        Assert.Null(again.ReadyTender);
        Assert.Equal("Pending", again.Status);
        Assert.Equal(2, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == ready.Id));
    }

    [Fact]
    public async Task WorkerWhoLostItsLeaseCannotOverwriteTheReclaimingWorker()
    {
        // worker-a claims the run with a 1s lease and no heartbeat, then blocks in a 3.5s LLM call.
        await using MarketplaceDbContext dbA = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN510001", 900_501));
        ScriptedSummaryGenerator slowSummary = new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(3500, cancellationToken);
            return Extraction();
        });
        SeapIngestionService workerA = CreateService(dbA, api, slowSummary, new WorkerTextReader("text"), new SeapOptions
        {
            RunLeaseSeconds = 1,
            RunHeartbeatIntervalSeconds = 0,
            MaxRunsPerTick = 5
        });
        SeapResolveResult? resolved = await workerA.ResolveNoticeByNumberAsync("SCN510001", CancellationToken.None);
        Guid tenderId = resolved!.TenderId;

        Task taskA = workerA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await using MarketplaceDbContext poll = CreateDatabase();
            return await poll.SeapIngestionRuns.AnyAsync(run => run.TenderId == tenderId
                && run.ClaimedBy == "worker-a" && run.Status == SeapIngestionStatus.Analyzing);
        }, TimeSpan.FromSeconds(5));

        // The 1s lease expires while worker-a is still in the LLM call.
        await Task.Delay(1600);

        await using (MarketplaceDbContext probe = CreateDatabase())
        {
            SeapIngestionRun row = await probe.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.TenderId == tenderId);
            Assert.True(row.LeaseExpiresAt is not null && row.LeaseExpiresAt < DateTimeOffset.UtcNow,
                $"expected an expired lease but saw status={row.Status} claimedBy={row.ClaimedBy} lease={row.LeaseExpiresAt} next={row.NextAttemptAt} attempts={row.AttemptCount}");
        }

        // worker-b reclaims the run (its own lease is long) and completes the analysis.
        await using MarketplaceDbContext dbB = CreateDatabase();
        SeapIngestionService workerB = CreateService(dbB, api, slowSummary, new WorkerTextReader("text"), new SeapOptions
        {
            RunLeaseSeconds = 120,
            MaxRunsPerTick = 5
        });
        Assert.Equal(1, await workerB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None));
        await taskA;

        await using MarketplaceDbContext verify = CreateDatabase();
        SeapTender fencedTender = await verify.SeapTenders.AsNoTracking().SingleAsync(tender => tender.NormalizedExternalId == "SCN510001");
        SeapIngestionRun run = await verify.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == fencedTender.Id);
        Assert.Equal(SeapIngestionStatus.Ready, run.Status);
        Assert.Equal(2, run.AttemptCount);            // worker-a claim + worker-b reclaim
        Assert.Equal(2, run.ClaimVersion);            // fencing token bumped exactly once per claim
        SeapTender tender = await verify.SeapTenders.AsNoTracking().Include(item => item.Attachments).SingleAsync(item => item.Id == fencedTender.Id);
        Assert.True(tender.SummaryGroundedInDocuments);
        Assert.Single(tender.Attachments);
        Assert.False(string.IsNullOrWhiteSpace(run.AnalysisAttemptId)); // worker-b's fresh attempt is what persisted
    }

    [Fact]
    public async Task HeartbeatRenewalPreventsReclaimDuringALongLlmCall()
    {
        await using MarketplaceDbContext dbA = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN510002", 900_502));
        ScriptedSummaryGenerator slowSummary = new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(3500, cancellationToken);
            return Extraction();
        });
        SeapIngestionService workerA = CreateService(dbA, api, slowSummary, new WorkerTextReader("text"), new SeapOptions
        {
            RunLeaseSeconds = 2,  // shorter than the LLM call — only the heartbeat keeps the lease alive
            RunHeartbeatIntervalSeconds = 0,
            MaxRunsPerTick = 5
        });
        await workerA.ResolveNoticeByNumberAsync("SCN510002", CancellationToken.None);

        Task taskA = workerA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);
        await WaitUntilAsync(async () =>
        {
            await using MarketplaceDbContext poll = CreateDatabase();
            return await poll.SeapIngestionRuns.AnyAsync(run => run.ClaimedBy == "worker-a"
                && run.Status == SeapIngestionStatus.Analyzing);
        }, TimeSpan.FromSeconds(5));

        // A separate lease-heartbeat actor renews worker-a's claim while the LLM call is in flight.
        Task renewalLoop = Task.Run(async () =>
        {
            while (!taskA.IsCompleted)
            {
                await using MarketplaceDbContext heartbeatDb = CreateDatabase();
                SeapIngestionService heartbeat = CreateService(heartbeatDb, api, slowSummary, new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
                await heartbeat.RenewActiveLeasesAsync("worker-a", CancellationToken.None);
                await Task.Delay(500);
            }
        });

        // Wait past the original 2s lease expiry while worker-a is still blocked in the LLM call, then check
        // that worker-b cannot reclaim the run — the heartbeat kept the lease alive.
        await Task.Delay(2500);
        await using MarketplaceDbContext dbB = CreateDatabase();
        SeapIngestionService workerB = CreateService(dbB, api, slowSummary, new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
        Assert.Equal(0, await workerB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None));

        await taskA;
        await renewalLoop;
        await using MarketplaceDbContext verify = CreateDatabase();
        SeapTender hbTender = await verify.SeapTenders.AsNoTracking().SingleAsync(tender => tender.NormalizedExternalId == "SCN510002");
        SeapIngestionRun run = await verify.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == hbTender.Id);
        Assert.Equal(SeapIngestionStatus.Ready, run.Status);
        Assert.Equal(1, run.AttemptCount);
        Assert.Equal(1, run.ClaimVersion);
    }


    [Fact]
    public async Task RetryableAnalysisFailuresStayActiveThenBecomeFailedAfterMaxAttempts()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN510003", 900_503));
        SeapOptions options = new() { MaxRunsPerTick = 5, MaxIngestionAttempts = 2 };
        ScriptedSummaryGenerator failing = new((_, _, _) => Task.FromResult<SeapAiExtraction?>(null));
        SeapIngestionService service = CreateService(db, api, failing, new WorkerTextReader("text"), options);
        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN510003", CancellationToken.None);
        Guid tenderId = resolved!.TenderId;

        // First attempt: transient LLM failure → the run stays ACTIVE (Analyzing), never terminal Partial.
        Assert.Equal(1, await service.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None));
        SeapIngestionRun afterFirst = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.TenderId == tenderId);
        Assert.Equal(SeapIngestionStatus.Analyzing, afterFirst.Status);
        Assert.NotEqual(SeapIngestionStatus.Partial, afterFirst.Status);
        Assert.NotEqual(SeapIngestionStatus.Failed, afterFirst.Status);
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.Equal("AnalysisFailed", afterFirst.LastErrorCode);
        Assert.NotNull(afterFirst.NextAttemptAt);
        string firstAttemptId = afterFirst.AnalysisAttemptId!;
        string requestHash = afterFirst.AnalysisRequestHash!;

        // Second attempt (max reached): the retryable failure becomes Failed and releases the capacity slot.
        await db.SeapIngestionRuns.Where(run => run.Id == afterFirst.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(run => run.NextAttemptAt, (DateTimeOffset?)null));
        Assert.Equal(1, await service.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None));
        SeapIngestionRun afterSecond = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.TenderId == tenderId);
        Assert.Equal(SeapIngestionStatus.Failed, afterSecond.Status);
        Assert.Equal(2, afterSecond.AttemptCount);
        Assert.NotNull(afterSecond.CompletedAt);
        Assert.Equal(0, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == tenderId && ActiveStatuses.Contains(run.Status)));
        Assert.NotEqual(firstAttemptId, afterSecond.AnalysisAttemptId); // every retry is a fresh attempt
        Assert.Equal(requestHash, afterSecond.AnalysisRequestHash);     // same logical analysis
    }

    [Fact]
    public async Task DocumentSetHashIsIdenticalRegardlessOfDocumentOrder()
    {
        await using MarketplaceDbContext dbA = CreateDatabase();
        WorkerSeapApiClient apiA = new(Notice("SCN510004", 900_504), _ =>
        [
            new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa.txt"),
            new SeapNoticeDocumentDto("CN-W/2", "CaietSarcini.pdf", "https://x/caiet.pdf")
        ]);
        SeapIngestionService serviceA = CreateService(dbA, apiA, new WorkerSummaryGenerator(), new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
        await serviceA.ResolveNoticeByNumberAsync("SCN510004", CancellationToken.None);
        await serviceA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);

        await using MarketplaceDbContext dbB = CreateDatabase();
        WorkerSeapApiClient apiB = new(Notice("SCN510005", 900_505), _ =>
        [
            new SeapNoticeDocumentDto("CN-W/2", "CaietSarcini.pdf", "https://x/caiet.pdf"),
            new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa.txt")
        ]);
        SeapIngestionService serviceB = CreateService(dbB, apiB, new WorkerSummaryGenerator(), new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
        await serviceB.ResolveNoticeByNumberAsync("SCN510005", CancellationToken.None);
        await serviceB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None);

        string hashA = (await dbA.SeapTenders.AsNoTracking().SingleAsync(tender => tender.NormalizedExternalId == "SCN510004")).DocumentSetHash!;
        string hashB = (await dbB.SeapTenders.AsNoTracking().SingleAsync(tender => tender.NormalizedExternalId == "SCN510005")).DocumentSetHash!;
        Assert.False(string.IsNullOrWhiteSpace(hashA));
        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public async Task CancellationTokenShutdownDoesNotMarkTheRunAsPermanentlyFailed()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN510006", 900_506));
        using CancellationTokenSource shutdown = new();
        ScriptedSummaryGenerator slowSummary = new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(60_000, cancellationToken); // cancels when the host shuts down
            return Extraction();
        });
        SeapIngestionService service = CreateService(db, api, slowSummary, new WorkerTextReader("text"), new SeapOptions
        {
            RunLeaseSeconds = 30,
            MaxRunsPerTick = 5
        });
        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN510006", CancellationToken.None);
        Guid tenderId = resolved!.TenderId;

        Task processing = service.ProcessPendingIngestionRunsAsync("worker-a", 5, shutdown.Token);
        await WaitUntilAsync(async () =>
        {
            await using MarketplaceDbContext poll = CreateDatabase();
            return await poll.SeapIngestionRuns.AnyAsync(run => run.TenderId == tenderId && run.Status == SeapIngestionStatus.Analyzing);
        }, TimeSpan.FromSeconds(5));

        shutdown.Cancel(); // graceful host shutdown mid-LLM
        await processing;

        await using MarketplaceDbContext verify = CreateDatabase();
        SeapIngestionRun run = await verify.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.NotEqual(SeapIngestionStatus.Failed, run.Status);
        Assert.NotEqual(SeapIngestionStatus.Partial, run.Status);
        Assert.Null(run.LastErrorCode);          // shutdown is not an error
        Assert.NotNull(run.ClaimedBy);           // lease expires naturally; another worker resumes later
    }

    private static readonly SeapIngestionStatus[] ActiveStatuses =
        [SeapIngestionStatus.Pending, SeapIngestionStatus.FetchingDocuments, SeapIngestionStatus.Analyzing];

    [Fact]
    public async Task SameUrlAndFileNameWithDifferentContentProduceDifferentHashesAndInvalidateTheAnalysis()
    {
        // Same upstream document id, same URL and same file name — only the published bytes differ.
        WorkerSeapApiClient apiA = new(Notice("SCN520001", 900_501), _ =>
            [new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa-date.txt")]);
        WorkerSeapApiClient apiB = new(Notice("SCN520002", 900_502), _ =>
            [new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa-date.txt")]);

        await using MarketplaceDbContext dbA = CreateDatabase();
        SeapIngestionService serviceA = CreateService(dbA, apiA, new WorkerSummaryGenerator(),
            new WorkerTextReader("FISA DE DATE — versiunea 1", "content-digest-v1"), new SeapOptions { MaxRunsPerTick = 5 });
        await serviceA.ResolveNoticeByNumberAsync("SCN520001", CancellationToken.None);
        await serviceA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);

        await using MarketplaceDbContext dbB = CreateDatabase();
        SeapIngestionService serviceB = CreateService(dbB, apiB, new WorkerSummaryGenerator(),
            new WorkerTextReader("FISA DE DATE — versiunea 2", "content-digest-v2"), new SeapOptions { MaxRunsPerTick = 5 });
        await serviceB.ResolveNoticeByNumberAsync("SCN520002", CancellationToken.None);
        await serviceB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None);

        SeapTender tenderA = await dbA.SeapTenders.AsNoTracking().Include(tender => tender.Attachments).SingleAsync(tender => tender.NormalizedExternalId == "SCN520001");
        SeapTender tenderB = await dbB.SeapTenders.AsNoTracking().Include(tender => tender.Attachments).SingleAsync(tender => tender.NormalizedExternalId == "SCN520002");
        Assert.NotNull(tenderA.Attachments.Single().ContentSha256);
        Assert.NotNull(tenderB.Attachments.Single().ContentSha256);
        // Same identity (URL/name/doc id), different bytes → different content-based set hash.
        Assert.False(string.IsNullOrWhiteSpace(tenderA.DocumentSetHash));
        Assert.NotEqual(tenderA.DocumentSetHash, tenderB.DocumentSetHash);
        // Both dossiers were fully downloaded/hashed → the digest marker says Complete (not Partial).
        Assert.Equal(SeapDocumentDigestStatus.Complete, tenderA.DocumentDigestStatus);
        Assert.Equal(SeapDocumentDigestStatus.Complete, tenderB.DocumentDigestStatus);

        // The stored analysis was grounded in the old content; publishing the new content invalidates it.
        SeapTender old = await dbA.SeapTenders.AsNoTracking().SingleAsync(tender => tender.NormalizedExternalId == "SCN520001");
        Assert.True(SeapTenderReadiness.IsDocumentAnalysisReady(old));
        old.RecordDocumentSetHash(tenderB.DocumentSetHash!, DateTimeOffset.UtcNow);
        Assert.False(SeapTenderReadiness.IsDocumentAnalysisReady(old));
        Assert.NotEqual(old.AnalyzedDocumentSetHash, old.DocumentSetHash);
    }

    [Fact]
    public async Task AbandonedProcessingIsNotKeptAliveByTheHeartbeatAndIsReclaimableAfterTheLeaseExpires()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN520003", 900_503));
        using CancellationTokenSource stopProcessing = new();
        ScriptedSummaryGenerator longLlm = new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(60_000, cancellationToken); // runs until the processing task is stopped
            return Extraction();
        });
        SeapIngestionService workerA = CreateService(db, api, longLlm, new WorkerTextReader("text"), new SeapOptions
        {
            RunLeaseSeconds = 2,
            MaxRunsPerTick = 5
        });
        SeapResolveResult? resolved = await workerA.ResolveNoticeByNumberAsync("SCN520003", CancellationToken.None);
        Guid tenderId = resolved!.TenderId;

        Task taskA = workerA.ProcessPendingIngestionRunsAsync("worker-a", 5, stopProcessing.Token);
        await WaitUntilAsync(async () =>
        {
            await using MarketplaceDbContext poll = CreateDatabase();
            return await poll.SeapIngestionRuns.AnyAsync(run => run.TenderId == tenderId
                && run.ClaimedBy == "worker-a" && run.Status == SeapIngestionStatus.Analyzing);
        }, TimeSpan.FromSeconds(5));

        // The lease-heartbeat stays alive for the whole scenario (like the hosted background worker) and keeps
        // renewing the lease WHILE the run is really being processed.
        using CancellationTokenSource keepHeartbeat = new();
        Task heartbeatLoop = Task.Run(async () =>
        {
            while (!keepHeartbeat.IsCancellationRequested)
            {
                await using MarketplaceDbContext heartbeatDb = CreateDatabase();
                SeapIngestionService heartbeat = CreateService(heartbeatDb, api, longLlm, new WorkerTextReader("text"), new SeapOptions { RunLeaseSeconds = 2 });
                await heartbeat.RenewActiveLeasesAsync("worker-a", CancellationToken.None);
                await Task.Delay(300);
            }
        });

        // The processing task ends UNEXPECTEDLY (host-stop style) without a terminal state: the run stays
        // active and claimed, but the per-run entry disappears from the active-run registry.
        stopProcessing.Cancel();
        await taskA;

        // The heartbeat keeps running (instance is alive) but can no longer renew this run — no zombie.
        await Task.Delay(600);
        Assert.Equal(0, await RenewCountAsync("worker-a", api, longLlm));

        // Once the lease expires (2s), another worker reclaims and completes the run.
        await WaitUntilAsync(async () =>
        {
            await using MarketplaceDbContext poll = CreateDatabase();
            DateTimeOffset? lease = await poll.SeapIngestionRuns.Where(run => run.TenderId == tenderId)
                .Select(run => run.LeaseExpiresAt).SingleAsync();
            return lease is null || lease <= DateTimeOffset.UtcNow;
        }, TimeSpan.FromSeconds(6));

        await using MarketplaceDbContext dbB = CreateDatabase();
        SeapIngestionService workerB = CreateService(dbB, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
        Assert.Equal(1, await workerB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None));
        keepHeartbeat.Cancel();
        try { await heartbeatLoop; } catch { /* best effort */ }

        SeapIngestionRun run = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.Equal(SeapIngestionStatus.Ready, run.Status);
        Assert.Equal(2, run.AttemptCount);
    }

    private static async Task<int> RenewCountAsync(string workerId, WorkerSeapApiClient api, ScriptedSummaryGenerator llm)
    {
        await using MarketplaceDbContext db = CreateDatabase();
        SeapIngestionService heartbeat = CreateService(db, api, llm, new WorkerTextReader("text"), new SeapOptions { MaxRunsPerTick = 5 });
        return await heartbeat.RenewActiveLeasesAsync(workerId, CancellationToken.None);
    }

    [Fact]
    public async Task LlmTimeoutIsBoundedAndRoutesToTheNormalRetryPath()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN520004", 900_504));
        ScriptedSummaryGenerator neverReturns = new(async (_, _, cancellationToken) =>
        {
            await Task.Delay(60_000, cancellationToken); // cut by the worker's own LLM timeout
            return Extraction();
        });
        SeapIngestionService service = CreateService(db, api, neverReturns, new WorkerTextReader("text"), new SeapOptions
        {
            LlmTimeoutSeconds = 1,
            MaxRunsPerTick = 5
        });
        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN520004", CancellationToken.None);
        Guid tenderId = resolved!.TenderId;

        // The LLM call is cut at its own 1s timeout → the run stays active (retryable), never terminal.
        int processed = await service.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);
        Assert.Equal(1, processed);
        SeapIngestionRun run = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.Equal("AnalysisTimeout", run.LastErrorCode);
        Assert.NotEqual(SeapIngestionStatus.Failed, run.Status);
        Assert.NotEqual(SeapIngestionStatus.Partial, run.Status);
        Assert.NotNull(run.NextAttemptAt); // normal bounded retry/backoff was scheduled
    }

    private static SeapTenderNoticeDto Notice(string noticeNo, int noticeId) => new(
        noticeNo, "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritatea Contractantă",
        DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20), [],
        County: "Dâmbovița", NoticeId: noticeId, SysNoticeTypeId: 17);

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, WorkerSummaryGenerator summary, WorkerTextReader reader) =>
        CreateService(db, api, summary, reader, new SeapOptions { MaxRunsPerTick = 5 });

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, WorkerSummaryGenerator summary, WorkerTextReader reader, SeapOptions options) =>
        new(db, api, new ThrowingStorage(), new NullCategorizer(), summary, reader,
            Options.Create(options), new FakeClock(), NullLogger<SeapIngestionService>.Instance);

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, ScriptedSummaryGenerator summary, WorkerTextReader reader, SeapOptions options) =>
        new(db, api, new ThrowingStorage(), new NullCategorizer(), summary, reader,
            Options.Create(options), new FakeClock(), NullLogger<SeapIngestionService>.Instance);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(150);
        }
        throw new TimeoutException("Condition was not met within the timeout.");
    }

    private static MarketplaceDbContext CreateDatabase() => new(TestDatabase.CreateOptions());

    private sealed class FakeClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }

    private sealed class ThrowingStorage : ISeapAttachmentStorage
    {
        public Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NullCategorizer : ISeapCategorizer
    {
        public Task<SeapCategorizationResult?> CategorizeAsync(string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken) =>
            Task.FromResult<SeapCategorizationResult?>(null);
    }

    private const string ExtractionJson = """
    {
      "summaryPoints": ["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"],
      "description": "Reparații la școală.",
      "awardCriterion": "Prețul cel mai scăzut",
      "participationGuarantee": "120.000 RON",
      "fundingSource": "Buget local",
      "contractDuration": "12 luni",
      "qualificationRequirements": "Minim 2 contracte similare",
      "keyPersonnel": "Manager de proiect",
      "certificates": "ISO 9001, ISO 45001"
    }
    """;

    private sealed class WorkerTextReader(string text, string? contentSha = null) : ISeapAttachmentTextReader
    {
        public int Reads { get; private set; }

        public Task<SeapAttachmentReadResult> ReadPublishedTextAsync(
            IReadOnlyCollection<SeapTenderAttachment> attachments, CancellationToken cancellationToken)
        {
            Reads++;
            IReadOnlyCollection<SeapAttachmentContentDigest> digests = contentSha is null
                ? []
                : attachments.Select(attachment => new SeapAttachmentContentDigest(
                    attachment.FileName, attachment.S3Url, contentSha, 2)).ToArray();
            return Task.FromResult(new SeapAttachmentReadResult(text, digests));
        }
    }

    private static SeapAiExtraction Extraction() => new(
        "Reparații la școală.", "Prețul cel mai scăzut", "120.000 RON", "Buget local",
        "12 luni", "Minim 2 contracte similare", "Manager de proiect", "ISO 9001, ISO 45001",
        ["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"]);

    /// <summary>Scriptable summary generator: each call invokes <paramref name="script"/> with the call index
    /// and the attachment text, so tests can inject delays, cancellations, transient nulls and failures.</summary>
    private sealed class ScriptedSummaryGenerator(Func<int, string?, CancellationToken, Task<SeapAiExtraction?>> script) : ISeapSummaryGenerator
    {
        public int Calls { get; private set; }
        public string? LastAttachmentText { get; private set; }

        public Task<SeapAiExtraction?> GenerateAsync(SeapTender tender, string? attachmentText, CancellationToken cancellationToken)
        {
            Calls++;
            LastAttachmentText = attachmentText;
            return script(Calls, attachmentText, cancellationToken);
        }
    }

    private sealed class WorkerSummaryGenerator : ISeapSummaryGenerator
    {
        public string? LastAttachmentText { get; private set; }

        public Task<SeapAiExtraction?> GenerateAsync(SeapTender tender, string? attachmentText, CancellationToken cancellationToken)
        {
            LastAttachmentText = attachmentText;
            return Task.FromResult<SeapAiExtraction?>(Extraction());
        }
    }

    private sealed class WorkerSeapApiClient(
        SeapTenderNoticeDto? notice,
        Func<int, IReadOnlyCollection<SeapNoticeDocumentDto>>? documentsFor = null) : ISeapApiClient
    {
        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
            Task.FromResult(notice);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream("FISA DE DATE — lucrări de reparații"u8.ToArray()));

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult(documentsFor is null
                ? (IReadOnlyCollection<SeapNoticeDocumentDto>)[
                    new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://e-licitatie.ro/api-pub/files/fisa-date.txt")]
                : documentsFor(noticeId));
    }
}