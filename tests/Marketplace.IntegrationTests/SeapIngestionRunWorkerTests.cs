using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// The worker/run-pipeline semantics against the shared PostgreSQL test container. This class intentionally
// hosts BOTH run purposes side by side so the split stays explicit:
//   • DigestAndAnalyze* tests (SCN50xxx/SCN51xxx/SCN52xxx) drive the digest pipeline (document fetch → text
//     extraction → grounded LLM analysis → ReadyForBidding). They seed the run EXPLICITLY as a
//     DigestAndAnalyze run (SeedDigestRunAsync) and never use the Direct Hit resolve as their setup
//     mechanism: a Direct Hit resolve now creates a Stage 1 DocumentImport run only (no text/LLM), while the
//     analysis action that creates DigestAndAnalyze runs belongs to a separate, later stage.
//   • DocumentImport*/DirectHitResolve* tests (SCN53xxx) exercise the Stage 1 Direct Hit contract: resolve →
//     one DocumentImport run → durable original-document download/storage → Ready/Partial, with NO text
//     extraction and NO LLM analysis, and a repeat resolve stays idempotent.
// Worker mechanics shared by both purposes (claim/lease/fencing/heartbeat/idempotency) are asserted on each.
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
        "SCN520001", "SCN520002", "SCN520003", "SCN520004", "SCN530001", "SCN530002", "SCN530003", "SCN530004", "SCN530005",
        "SCN540001", "SCN540002"
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
    public async Task DigestAndAnalyzeRunInvokesExtractionAndSummaryGeneration()
    {
        await using MarketplaceDbContext db = CreateDatabase();

        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500001", "Reparații la școală", null, ["45214100-1"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
            [], County: "Dâmbovița", NoticeId: 900_501, SysNoticeTypeId: 17));
        WorkerSummaryGenerator summary = new();
        SeapIngestionService service = CreateService(db, api, summary, new WorkerTextReader("FISA DE DATE — lucrări de reparații\nGaranție de participare: 120.000 RON"));

        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN500001");
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
    public async Task DigestAndAnalyzeHistoricalRunReachesReadyButIsNeverPromotedToABidTarget()
    {
        await using MarketplaceDbContext db = CreateDatabase();

        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500002", "Reparații la școală (istoric)", null, ["45214100-1"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(-5),
            [], County: "Dâmbovița", NoticeId: 900_502, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        await SeedDigestRunAsync(db, api.Notice!, "SCN500002");
        int processed = await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
        Assert.Equal(1, processed);

        SeapTender saved = await db.SeapTenders.SingleAsync(tender => tender.NormalizedExternalId == "SCN500002");
        Assert.Equal(SeapImportMode.DirectHistoricalLookup, saved.ImportMode);
        Assert.True(saved.SummaryGroundedInDocuments);
        // Research history: even with the analysis completed it is NOT an active bid target.
        Assert.Equal(SeapTenderStatus.Imported, saved.Status);
    }

    [Fact]
    public async Task DigestAndAnalyzeActiveRunWithUnexpiredLeaseCannotBeClaimedButIsResumedAfterExpiry()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500003", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_503, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN500003");
        Guid runId = resolved.RunId!.Value;

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
    public async Task DigestAndAnalyzeFailedRunAllowsAFreshRun()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500004", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_504, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult first = await SeedDigestRunAsync(db, api.Notice!, "SCN500004");
        Guid tenderId = first.TenderId;
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
    public async Task DigestAndAnalyzeChangedDocumentSetInvalidatesTheStoredAnalysisAndTriggersARerun()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
            "SCN500005", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritate",
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [], NoticeId: 900_505, SysNoticeTypeId: 17));
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN500005");
        await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
        SeapTender ready = await db.SeapTenders.SingleAsync(tender => tender.Id == resolved.TenderId);
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
    public async Task DigestAndAnalyzeWorkerWhoLostItsLeaseCannotOverwriteTheReclaimingWorker()
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
        SeapResolveResult resolved = await SeedDigestRunAsync(dbA, api.Notice!, "SCN510001");
        Guid tenderId = resolved.TenderId;

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
    public async Task DigestAndAnalyzeHeartbeatRenewalPreventsReclaimDuringALongLlmCall()
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
        await SeedDigestRunAsync(dbA, api.Notice!, "SCN510002");

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
    public async Task DigestAndAnalyzeRetryableAnalysisFailuresStayActiveThenBecomeFailedAfterMaxAttempts()
    {
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(Notice("SCN510003", 900_503));
        SeapOptions options = new() { MaxRunsPerTick = 5, MaxIngestionAttempts = 2 };
        ScriptedSummaryGenerator failing = new((_, _, _) => Task.FromResult<SeapAiExtraction?>(null));
        SeapIngestionService service = CreateService(db, api, failing, new WorkerTextReader("text"), options);
        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN510003");
        Guid tenderId = resolved.TenderId;

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
    public async Task DigestAndAnalyzeCancellationTokenShutdownDoesNotMarkTheRunAsPermanentlyFailed()
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
        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN510006");
        Guid tenderId = resolved.TenderId;

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
    public async Task DigestAndAnalyzeSameUrlAndFileNameWithDifferentContentProduceDifferentHashesAndInvalidateTheAnalysis()
    {
        // Same upstream document id, same URL and same file name — only the published bytes differ.
        WorkerSeapApiClient apiA = new(Notice("SCN520001", 900_501), _ =>
            [new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa-date.txt")]);
        WorkerSeapApiClient apiB = new(Notice("SCN520002", 900_502), _ =>
            [new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://x/fisa-date.txt")]);

        await using MarketplaceDbContext dbA = CreateDatabase();
        SeapIngestionService serviceA = CreateService(dbA, apiA, new WorkerSummaryGenerator(),
            new WorkerTextReader("FISA DE DATE — versiunea 1", "content-digest-v1"), new SeapOptions { MaxRunsPerTick = 5 });
        await SeedDigestRunAsync(dbA, apiA.Notice!, "SCN520001");
        await serviceA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);

        await using MarketplaceDbContext dbB = CreateDatabase();
        SeapIngestionService serviceB = CreateService(dbB, apiB, new WorkerSummaryGenerator(),
            new WorkerTextReader("FISA DE DATE — versiunea 2", "content-digest-v2"), new SeapOptions { MaxRunsPerTick = 5 });
        await SeedDigestRunAsync(dbB, apiB.Notice!, "SCN520002");
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
    public async Task DigestAndAnalyzeAbandonedProcessingIsNotKeptAliveByTheHeartbeatAndIsReclaimableAfterTheLeaseExpires()
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
        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN520003");
        Guid tenderId = resolved.TenderId;

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
    public async Task DigestAndAnalyzeLlmTimeoutIsBoundedAndRoutesToTheNormalRetryPath()
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
        SeapResolveResult resolved = await SeedDigestRunAsync(db, api.Notice!, "SCN520004");
        Guid tenderId = resolved.TenderId;

        // The LLM call is cut at its own 1s timeout → the run stays active (retryable), never terminal.
        int processed = await service.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);
        Assert.Equal(1, processed);
        SeapIngestionRun run = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.Equal("AnalysisTimeout", run.LastErrorCode);
        Assert.NotEqual(SeapIngestionStatus.Failed, run.Status);
        Assert.NotEqual(SeapIngestionStatus.Partial, run.Status);
        Assert.NotNull(run.NextAttemptAt); // normal bounded retry/backoff was scheduled
    }

    [Fact]
    public async Task DirectHitResolveCreatesDocumentImportAndNeverInvokesDigestPipeline()
    {
        // Stage 1 (Direct Hit dossier): a resolve creates ONE DocumentImport run that durably stores the
        // original published documents (per-file Downloaded/Failed + functional group) and reports Ready —
        // with NO text extraction and NO LLM analysis, and a repeat resolve stays idempotent (no second run).
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-docimport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            await using MarketplaceDbContext db = CreateDatabase();

            WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
                "SCN530001", "Lucrari reparatii scoala", null, ["45214100-1"], 2_420_466m, "RON",
                "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
                [], County: "Dambovita", NoticeId: 900_530, SysNoticeTypeId: 17));
            IOptions<LocalStorageOptions> localStorage =
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService objectStorage =
                new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOriginalDocumentStore dossier =
                new(objectStorage, Options.Create(new SeapStorageOptions { Enabled = false }), localStorage,
                    Options.Create(new SeapOptions { MaxOriginalDocumentBytes = 1024L * 1024, MaxTenderOriginalDocumentsBytes = 64L * 1024 * 1024 }),
                    NullLogger<SeapOriginalDocumentStore>.Instance);
            SeapIngestionService service = CreateService(db, api, dossier, objectStorage);

            SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN530001", CancellationToken.None);
            Assert.NotNull(resolved);
            Assert.Equal("Pending", resolved.Status);
            Assert.Equal("DocumentImport", resolved.Purpose);

            int processed = await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);
            Assert.Equal(1, processed);

            SeapIngestionRun run = await db.SeapIngestionRuns.SingleAsync(item => item.NoticeNo == "SCN530001");
            Assert.Equal(SeapIngestionRunPurpose.DocumentImport, run.Purpose);
            Assert.Equal(SeapIngestionStatus.Ready, run.Status);
            Assert.NotNull(run.DocumentsFetchedAt);
            Assert.Null(run.TextExtractedAt); // Stage 1 never extracts text / never runs the LLM
            Assert.NotNull(run.CompletedAt);

            SeapTender saved = await db.SeapTenders
                .Include(tender => tender.Attachments)
                .SingleAsync(tender => tender.NormalizedExternalId == "SCN530001");
            SeapTenderAttachment attachment = Assert.Single(saved.Attachments);
            Assert.Equal(SeapDocumentDownloadStatus.Downloaded, attachment.DownloadStatus);
            Assert.Equal(SeapDocumentGroup.TenderDocumentation, attachment.DocumentGroup);
            // The SEAP source URL is preserved as provenance while the durable dossier copy lives separately
            // under its own storage reference — never mixed, never renamed to look like an external URL.
            Assert.Equal("https://e-licitatie.ro/api-pub/files/fisa-date.txt", attachment.SourceUrl);
            Assert.StartsWith("local://", attachment.StorageReference); // durable dossier copy, not the remote URL
            Assert.False(string.IsNullOrWhiteSpace(attachment.ContentSha256));
            Assert.Equal(SeapDocumentDigestStatus.Complete, saved.DocumentDigestStatus);
            StorageObject? stored = await objectStorage.OpenReadAsync(attachment.StorageReference!, CancellationToken.None);
            Assert.NotNull(stored); // the durable dossier copy actually exists and is readable

            // A second resolve of the same notice is idempotent at Stage 1: same dossier status, no new run.
            SeapResolveResult? second = await service.ResolveNoticeByNumberAsync("SCN530001", CancellationToken.None);
            Assert.NotNull(second);
            Assert.Equal(SeapIngestionStatus.Ready.ToString(), second.Status);
            Assert.Equal("DocumentImport", second.Purpose);
            Assert.Equal(1, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == saved.Id));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup of the temp dossier */ }
        }
    }


    [Fact]
    public async Task DocumentImportClassifiesDuaeBySectionAndFileName()
    {
        // The functional dossier groups follow the SEAP section when the payload exposes one and fall back to
        // the published file name — the dedicated „Documente DUAE" section and the DUAE_CERERE_*.xml forms both
        // land in SeapDocumentGroup.Duae.
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-docimport-duae-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            await using MarketplaceDbContext db = CreateDatabase();
            WorkerSeapApiClient api = new(
                new SeapTenderNoticeDto(
                    "SCN530002", "Lucrari reparatii scoala", null, ["45214100-1"], 2_420_466m, "RON",
                    "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
                    [], County: "Dambovita", NoticeId: 900_532, SysNoticeTypeId: 17),
                documentsFor: _ =>
                [
                    new SeapNoticeDocumentDto("D1", "Caiet de sarcini.pdf", "https://e-licitatie.ro/api-pub/files/d1.pdf", Section: null),
                    new SeapNoticeDocumentDto("D2", "DUAE_CERERE_1.xml", "https://e-licitatie.ro/api-pub/files/d2.xml", Section: null),
                    // Section-driven classification wins over the file name (the name itself is not a DUAE hint).
                    new SeapNoticeDocumentDto("D3", "Nota explicativa.pdf", "https://e-licitatie.ro/api-pub/files/d3.pdf", Section: "Documente DUAE")
                ]);
            IOptions<LocalStorageOptions> localStorage =
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService objectStorage =
                new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOptions seapOptions = new()
            {
                MaxOriginalDocumentBytes = 1024L * 1024,
                MaxTenderOriginalDocumentsBytes = 64L * 1024 * 1024,
                MaxRunsPerTick = 5
            };
            SeapOriginalDocumentStore dossier =
                new(objectStorage, Options.Create(new SeapStorageOptions { Enabled = false }), localStorage,
                    Options.Create(seapOptions), NullLogger<SeapOriginalDocumentStore>.Instance);
            SeapIngestionService service = CreateService(db, api, dossier, objectStorage, seapOptions);

            SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN530002", CancellationToken.None);
            Assert.NotNull(resolved);
            await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);

            SeapTender saved = await db.SeapTenders
                .Include(tender => tender.Attachments)
                .SingleAsync(tender => tender.NormalizedExternalId == "SCN530002");
            Assert.Equal(SeapDocumentGroup.Duae, saved.Attachments.Single(a => a.SourceDocumentId == "D2").DocumentGroup);
            Assert.Equal(SeapDocumentGroup.Duae, saved.Attachments.Single(a => a.SourceDocumentId == "D3").DocumentGroup);
            Assert.Equal(SeapDocumentGroup.TenderDocumentation, saved.Attachments.Single(a => a.SourceDocumentId == "D1").DocumentGroup);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup of the temp dossier */ }
        }
    }

    [Fact]
    public async Task DocumentImportMarksOversizedFileFailedAndLeavesNoPartialObject()
    {
        // Seap:MaxOriginalDocumentBytes is enforced while streaming: an over-limit original becomes Failed with
        // an explicit code and the storage layer removes the partially written object.
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-docimport-too-large-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            await using MarketplaceDbContext db = CreateDatabase();
            WorkerSeapApiClient api = new(new SeapTenderNoticeDto(
                "SCN530003", "Lucrari reparatii scoala", null, ["45214100-1"], 2_420_466m, "RON",
                "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
                [], County: "Dambovita", NoticeId: 900_533, SysNoticeTypeId: 17));
            IOptions<LocalStorageOptions> localStorage =
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService objectStorage =
                new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOptions seapOptions = new()
            {
                MaxOriginalDocumentBytes = 8,
                MaxTenderOriginalDocumentsBytes = 1024L * 1024,
                MaxRunsPerTick = 5
            };
            SeapOriginalDocumentStore dossier =
                new(objectStorage, Options.Create(new SeapStorageOptions { Enabled = false }), localStorage,
                    Options.Create(seapOptions), NullLogger<SeapOriginalDocumentStore>.Instance);
            SeapIngestionService service = CreateService(db, api, dossier, objectStorage, seapOptions);

            await service.ResolveNoticeByNumberAsync("SCN530003", CancellationToken.None);
            await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);

            SeapTender saved = await db.SeapTenders
                .Include(tender => tender.Attachments)
                .SingleAsync(tender => tender.NormalizedExternalId == "SCN530003");
            SeapTenderAttachment attachment = Assert.Single(saved.Attachments);
            Assert.Equal(SeapDocumentDownloadStatus.Failed, attachment.DownloadStatus);
            Assert.Equal("OriginalDocumentTooLarge", attachment.DownloadErrorCode);
            SeapIngestionRun run = await db.SeapIngestionRuns.SingleAsync(item => item.NoticeNo == "SCN530003");
            Assert.Equal(SeapIngestionStatus.Failed, run.Status);
            // The over-limit upload was aborted before a durable object was left behind.
            Assert.Empty(Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup of the temp dossier */ }
        }
    }

    [Fact]
    public async Task DocumentImportDossierQuotaDeletesTheOverQuotaObjectAndMarksItFailed()
    {
        // Seap:MaxTenderOriginalDocumentsBytes bounds the whole dossier: the file that pushes the tender past the
        // cap is deleted again and marked Failed (DossierQuotaExceeded) — the dossier never silently overgrows.
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-docimport-quota-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            await using MarketplaceDbContext db = CreateDatabase();
            WorkerSeapApiClient api = new(
                new SeapTenderNoticeDto(
                    "SCN530004", "Lucrari reparatii scoala", null, ["45214100-1"], 2_420_466m, "RON",
                    "Primaria Comunei Poiana Dambovita", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20),
                    [], County: "Dambovita", NoticeId: 900_534, SysNoticeTypeId: 17),
                documentsFor: _ =>
                [
                    new SeapNoticeDocumentDto("Q1", "FisaDate.txt", "https://e-licitatie.ro/api-pub/files/q1.txt", Section: null),
                    new SeapNoticeDocumentDto("Q2", "Caiet.txt", "https://e-licitatie.ro/api-pub/files/q2.txt", Section: null)
                ]);
            byte[] payload = "FISA DE DATE — lucrări de reparații"u8.ToArray();
            long cap = payload.LongLength + 1; // exactly one file fits inside the tender dossier cap
            IOptions<LocalStorageOptions> localStorage =
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService objectStorage =
                new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOptions seapOptions = new()
            {
                MaxConcurrentDocumentDownloads = 1,
                MaxOriginalDocumentBytes = 1024L * 1024,
                MaxTenderOriginalDocumentsBytes = cap,
                MaxRunsPerTick = 5
            };
            SeapOriginalDocumentStore dossier =
                new(objectStorage, Options.Create(new SeapStorageOptions { Enabled = false }), localStorage,
                    Options.Create(seapOptions), NullLogger<SeapOriginalDocumentStore>.Instance);
            SeapIngestionService service = CreateService(db, api, dossier, objectStorage, seapOptions);

            await service.ResolveNoticeByNumberAsync("SCN530004", CancellationToken.None);
            await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None);

            SeapTender saved = await db.SeapTenders
                .Include(tender => tender.Attachments)
                .SingleAsync(tender => tender.NormalizedExternalId == "SCN530004");
            SeapTenderAttachment stored = saved.Attachments.Single(a => a.DownloadStatus == SeapDocumentDownloadStatus.Downloaded);
            SeapTenderAttachment overQuota = saved.Attachments.Single(a => a.DownloadStatus == SeapDocumentDownloadStatus.Failed);
            Assert.Equal("DossierQuotaExceeded", overQuota.DownloadErrorCode);
            Assert.False(string.IsNullOrWhiteSpace(stored.StorageReference));
            StorageObject? exists = await objectStorage.OpenReadAsync(stored.StorageReference!, CancellationToken.None);
            Assert.NotNull(exists); // the first file that fit within the cap is durably stored
            await exists!.Content.DisposeAsync();
            // Exactly one dossier object remains — the over-quota object was deleted, not left behind.
            Assert.Single(Directory.GetFiles(tempRoot, "*", SearchOption.AllDirectories));
            SeapIngestionRun run = await db.SeapIngestionRuns.SingleAsync(item => item.NoticeNo == "SCN530004");
            Assert.Equal(SeapIngestionStatus.Partial, run.Status);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup of the temp dossier */ }
        }
    }

    [Fact]
    public async Task DocumentImportWorkerWhoLostItsLeaseCannotStampDownloaded()
    {
        // Stage 1 fencing: the per-file Downloaded stamp and the run completion are persisted by ONE fenced
        // save at the end of the DocumentImport pipeline. A worker whose 1s lease expires while it is blocked in
        // the (slow) dossier store can never overwrite the reclaiming worker's results — its fenced save fails
        // and the whole checkpoint transaction (including the attachment stamps) is rolled back.
        string tempRoot = Path.Combine(Path.GetTempPath(), "seap-docimport-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            await using MarketplaceDbContext dbA = CreateDatabase();
            WorkerSeapApiClient api = new(Notice("SCN530005", 900_535));
            IOptions<LocalStorageOptions> localStorage =
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService objectStorage =
                new(localStorage, NullLogger<LocalFileStorageService>.Instance);
            SeapOriginalDocumentStore dossier = new(objectStorage,
                Options.Create(new SeapStorageOptions { Enabled = false }), localStorage,
                Options.Create(new SeapOptions
                {
                    RunLeaseSeconds = 1,
                    RunHeartbeatIntervalSeconds = 0,
                    MaxRunsPerTick = 5,
                    MaxOriginalDocumentBytes = 1024L * 1024,
                    MaxTenderOriginalDocumentsBytes = 1024L * 1024
                }),
                NullLogger<SeapOriginalDocumentStore>.Instance);
            SlowOriginalDocumentStore slowDossier = new(dossier);
            SeapIngestionService workerA = CreateService(dbA, api, slowDossier, objectStorage, new SeapOptions
            {
                RunLeaseSeconds = 1,
                RunHeartbeatIntervalSeconds = 0,
                MaxRunsPerTick = 5
            });
            SeapResolveResult? resolved = await workerA.ResolveNoticeByNumberAsync("SCN530005", CancellationToken.None);
            Guid tenderId = resolved!.TenderId;

            Task taskA = workerA.ProcessPendingIngestionRunsAsync("worker-a", 5, CancellationToken.None);
            await WaitUntilAsync(async () =>
            {
                await using MarketplaceDbContext poll = CreateDatabase();
                SeapIngestionRun row = await poll.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.TenderId == tenderId);
                return row.ClaimedBy == "worker-a" && row.DocumentsFetchedAt is not null
                    && row.Status == SeapIngestionStatus.FetchingDocuments;
            }, TimeSpan.FromSeconds(5));

            // The 1s lease expires while worker-a is still blocked in the slow dossier store.
            await Task.Delay(1600);
            await using (MarketplaceDbContext probe = CreateDatabase())
            {
                SeapIngestionRun row = await probe.SeapIngestionRuns.AsNoTracking().SingleAsync(run => run.TenderId == tenderId);
                Assert.True(row.LeaseExpiresAt is not null && row.LeaseExpiresAt < DateTimeOffset.UtcNow,
                    $"expected an expired lease but saw status={row.Status} claimedBy={row.ClaimedBy} lease={row.LeaseExpiresAt}");
            }

            // worker-b reclaims the run and completes the DocumentImport dossier while worker-a is still blocked.
            await using MarketplaceDbContext dbB = CreateDatabase();
            SeapIngestionService workerB = CreateService(dbB, api, dossier, objectStorage, new SeapOptions
            {
                RunLeaseSeconds = 120,
                MaxRunsPerTick = 5
            });
            Assert.Equal(1, await workerB.ProcessPendingIngestionRunsAsync("worker-b", 5, CancellationToken.None));
            await taskA; // worker-a resumes, its fenced save fails and it abandons — nothing it staged is saved

            await using MarketplaceDbContext verify = CreateDatabase();
            SeapIngestionRun run = await verify.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
            Assert.Equal(SeapIngestionStatus.Ready, run.Status);
            Assert.Equal(2, run.AttemptCount); // worker-a claim + worker-b reclaim
            Assert.Equal(2, run.ClaimVersion); // fencing token bumped exactly once per claim
            Assert.True(run.CompletedAt is not null);
            SeapTender tender = await verify.SeapTenders.AsNoTracking()
                .Include(item => item.Attachments).SingleAsync(item => item.Id == tenderId);
            SeapTenderAttachment attachment = Assert.Single(tender.Attachments);
            // Only worker-b's successful store is visible: worker-a's in-flight stamp was rolled back with its
            // lost-lease fenced transaction.
            Assert.Equal(SeapDocumentDownloadStatus.Downloaded, attachment.DownloadStatus);
            Assert.False(string.IsNullOrWhiteSpace(attachment.StorageReference));
            Assert.False(string.IsNullOrWhiteSpace(attachment.ContentSha256));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); }
            catch { /* best-effort cleanup of the temp dossier (worker-a's orphaned partial object may linger) */ }
        }
    }


    [Fact]
    public async Task DocumentImportManifest400FailsTheRunWithSeapDocumentManifestRejected()
    {
        // A rejected manifest (HTTP 400 — e.g. an SCN queried by its noticeId instead of its cNoticeId) must
        // FAIL the run with an explicit code: the empty dossier cannot be a misleading Partial, and a
        // contract/request error is never retried endlessly. The run must also resolve the manifest by the
        // cNoticeId it persisted, not by the plain noticeId.
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(
            new SeapTenderNoticeDto(
                "SCN540001", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritatea",
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [],
                NoticeId: 540_001, SysNoticeTypeId: 17, CNoticeId: 5_400_001),
            manifestRejected: true);
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN540001", CancellationToken.None);
        Assert.NotNull(resolved);
        Guid tenderId = resolved!.TenderId;

        Assert.Equal(1, await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None));

        SeapIngestionRun run = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.Equal(SeapIngestionStatus.Failed, run.Status);
        Assert.Equal(SeapIngestionService.ErrorSeapDocumentManifestRejected, run.LastErrorCode);
        Assert.NotNull(run.CompletedAt);
        Assert.Equal(1, run.AttemptCount); // rejected once — no endless retry
        Assert.Null(run.ClaimedBy);

        SeapTender tender = await db.SeapTenders.AsNoTracking().Include(item => item.Attachments).SingleAsync(item => item.Id == tenderId);
        Assert.Empty(tender.Attachments);
        Assert.Equal(5_400_001, api.LastManifestId); // manifest resolved by cNoticeId

        // A terminal Failed run is not reprocessed and does not spin new attempts.
        Assert.Equal(0, await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None));
    }

    [Fact]
    public async Task DocumentImportEmptyManifestIsReadyWithAnEmptyDossier()
    {
        // A manifest that WAS obtained and genuinely publishes zero documents is Ready with an empty list —
        // never Partial, never a false download failure.
        await using MarketplaceDbContext db = CreateDatabase();
        WorkerSeapApiClient api = new(
            new SeapTenderNoticeDto(
                "SCN540002", "Lucrări", null, ["45214100-1"], 1000m, "RON", "Autoritatea",
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(20), [],
                NoticeId: 540_002, SysNoticeTypeId: 17, CNoticeId: 5_400_002),
            emptyManifest: true);
        SeapIngestionService service = CreateService(db, api, new WorkerSummaryGenerator(), new WorkerTextReader("text"));

        SeapResolveResult? resolved = await service.ResolveNoticeByNumberAsync("SCN540002", CancellationToken.None);
        Assert.NotNull(resolved);
        Guid tenderId = resolved!.TenderId;

        Assert.Equal(1, await service.ProcessPendingIngestionRunsAsync("worker-1", 5, CancellationToken.None));

        SeapIngestionRun run = await db.SeapIngestionRuns.AsNoTracking().SingleAsync(item => item.TenderId == tenderId);
        Assert.Equal(SeapIngestionStatus.Ready, run.Status);
        Assert.NotNull(run.CompletedAt);
        Assert.NotNull(run.DocumentsFetchedAt);
        Assert.Null(run.LastErrorCode);

        SeapTender tender = await db.SeapTenders.AsNoTracking().Include(item => item.Attachments).SingleAsync(item => item.Id == tenderId);
        Assert.Empty(tender.Attachments);
        // Empty document set → the deterministic set hash is the SHA-256 of the empty payload.
        Assert.StartsWith("e3b0c44298fc1c149afbf4c8996fb924", tender.DocumentSetHash, StringComparison.Ordinal);
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
        new(db, api, new ThrowingStorage(), new NullOriginalDocumentStore(), new NullObjectStorage(), new NullCategorizer(), summary, reader,
            Options.Create(options), new FakeClock(), NullLogger<SeapIngestionService>.Instance);

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, ScriptedSummaryGenerator summary, WorkerTextReader reader, SeapOptions options) =>
        new(db, api, new ThrowingStorage(), new NullOriginalDocumentStore(), new NullObjectStorage(), new NullCategorizer(), summary, reader,
            Options.Create(options), new FakeClock(), NullLogger<SeapIngestionService>.Instance);

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, ISeapOriginalDocumentStore originalDocumentStore, IStorageService objectStorage) =>
        new(db, api, new ThrowingStorage(), originalDocumentStore, objectStorage, new NullCategorizer(),
            new WorkerSummaryGenerator(), new WorkerTextReader("text"),
            Options.Create(new SeapOptions { MaxRunsPerTick = 5 }), new FakeClock(),
            NullLogger<SeapIngestionService>.Instance);

    private static SeapIngestionService CreateService(
        MarketplaceDbContext db, WorkerSeapApiClient api, ISeapOriginalDocumentStore originalDocumentStore,
        IStorageService objectStorage, SeapOptions options) =>
        new(db, api, new ThrowingStorage(), originalDocumentStore, objectStorage, new NullCategorizer(),
            new WorkerSummaryGenerator(), new WorkerTextReader("text"), Options.Create(options), new FakeClock(),
            NullLogger<SeapIngestionService>.Instance);

    /// <summary>Seeds a tender + ONE explicit <see cref="SeapIngestionRunPurpose.DigestAndAnalyze"/> run,
    /// mirroring the (separate, later-stage) analysis action. A Direct Hit resolve creates Stage 1
    /// DocumentImport runs only (durable original-document download, no text/LLM), so the digest-pipeline
    /// tests must never use resolve as their setup — the run purpose is created explicitly right here.</summary>
    private static async Task<SeapResolveResult> SeedDigestRunAsync(
        MarketplaceDbContext db, SeapTenderNoticeDto notice, string noticeNo)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapTender tender = SeapTenderMapper.MapNotice(notice, now);
        SeapImportMode importMode = notice.Deadline < now ? SeapImportMode.DirectHistoricalLookup : SeapImportMode.OnDemand;
        tender.RecordResolveAttempt(importMode, now);
        SeapIngestionRun run = SeapIngestionRun.Start(tender.Id, noticeNo, now, SeapIngestionRunPurpose.DigestAndAnalyze);
        db.SeapTenders.Add(tender);
        db.SeapIngestionRuns.Add(run);
        await db.SaveChangesAsync();
        return new(tender.Id, tender.NormalizedExternalId, run.Status.ToString(), run.Id, null,
            SeapIngestionRunPurpose.DigestAndAnalyze.ToString());
    }

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
                    attachment.FileName,
                    attachment.SourceDocumentId ?? attachment.SourceUrl ?? attachment.StorageReference ?? string.Empty,
                    contentSha, 2)).ToArray();
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

    private sealed class WorkerSeapApiClient : ISeapApiClient
    {
        private readonly SeapTenderNoticeDto? _notice;
        private readonly Func<int, IReadOnlyCollection<SeapNoticeDocumentDto>>? _documentsFor;
        private readonly bool _manifestRejected;
        private readonly bool _emptyManifest;

        public WorkerSeapApiClient(
            SeapTenderNoticeDto? notice,
            Func<int, IReadOnlyCollection<SeapNoticeDocumentDto>>? documentsFor = null,
            bool manifestRejected = false,
            bool emptyManifest = false)
        {
            _notice = notice;
            _documentsFor = documentsFor;
            _manifestRejected = manifestRejected;
            _emptyManifest = emptyManifest;
        }

        public SeapTenderNoticeDto? Notice => _notice;

        /// <summary>Last manifest identifier the worker passed to <c>GetDfNoticeSectionFiles</c> — used to
        /// assert that an SCN run resolves its manifest by cNoticeId, not by the plain noticeId.</summary>
        public int? LastManifestId { get; private set; }
        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
            Task.FromResult(_notice);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream("FISA DE DATE — lucrări de reparații"u8.ToArray()));

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken)
        {
            LastManifestId = noticeId;
            if (_manifestRejected)
                throw new SeapDocumentManifestRejectedException("Manifest respins de SEAP (HTTP 400 simulat).");
            if (_emptyManifest)
                return Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
            return Task.FromResult(_documentsFor is null
                ? (IReadOnlyCollection<SeapNoticeDocumentDto>)[
                    new SeapNoticeDocumentDto("CN-W/1", "FisaDate.txt", "https://e-licitatie.ro/api-pub/files/fisa-date.txt")]
                : _documentsFor(noticeId));
        }
    }

    /// <summary>Dossier-store decorator that delays the durable store call once, so a worker can lose its lease
    /// while it is mid-download (the fencing test then proves the stale worker cannot persist its stamps).</summary>
    private sealed class SlowOriginalDocumentStore(ISeapOriginalDocumentStore inner) : ISeapOriginalDocumentStore
    {
        public async Task<StoredSeapOriginalDocument> StoreOriginalAsync(
            Guid tenderId, string fileName, Stream content, CancellationToken cancellationToken)
        {
            await Task.Delay(3500, cancellationToken);
            return await inner.StoreOriginalAsync(tenderId, fileName, content, cancellationToken);
        }

        public Task<SeapOriginalDocumentContent?> OpenOriginalAsync(string reference, CancellationToken cancellationToken) =>
            inner.OpenOriginalAsync(reference, cancellationToken);

        public Task DeleteOriginalAsync(string reference, CancellationToken cancellationToken) =>
            inner.DeleteOriginalAsync(reference, cancellationToken);
    }
}