using Marketplace.Application.Seap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// Livrarea 2 background worker for resolve runs. Gated by the same hot-reloadable Features:SeapIngestionEnabled
// switch as the notice poller; while disabled it simply idles. When enabled it ticks every
// Seap:RunProcessingIntervalSeconds and asks the ingestion service to claim/process a bounded batch of active
// runs (atomic lease claim → fetch documents → extract → analyze). Runs survive restarts through persistent
// per-stage checkpoints, so a process dying between stages never re-downloads documents or repeats an LLM call.
public sealed partial class SeapIngestionRunWorker(
    IServiceScopeFactory scopeFactory,
    SeapWorkerIdentity workerIdentity,
    IOptionsMonitor<FeaturesOptions> features,
    IOptionsMonitor<SeapOptions> seapOptions,
    ILogger<SeapIngestionRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!features.CurrentValue.SeapIngestionWorkerEnabled)
            {
                await Delay(TimeSpan.FromSeconds(60), stoppingToken);
                continue;
            }

            try
            {
                // One run per scope/tick: each run is claimed and processed in its own fresh DI scope +
                // DbContext, immediately before it is processed — a run never waits in a batch claim while
                // its lease expires, and a checkpoint transaction never shares a DbContext with another run.
                int budget = Math.Max(1, seapOptions.CurrentValue.MaxRunsPerTick);
                int processed = 0;
                while (budget-- > 0)
                {
                    using IServiceScope scope = scopeFactory.CreateScope();
                    ISeapIngestionService service = scope.ServiceProvider.GetRequiredService<ISeapIngestionService>();
                    int batch = await service.ProcessPendingIngestionRunsAsync(
                        workerIdentity.Id, 1, stoppingToken);
                    if (batch == 0) break;
                    processed += batch;
                }
                if (processed > 0)
                    LogSeapRunBatchProcessed(logger, processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSeapRunBatchFailed(logger, exception);
            }

            await Delay(TimeSpan.FromSeconds(Math.Max(5, seapOptions.CurrentValue.RunProcessingIntervalSeconds)), stoppingToken);
        }
    }

    private static async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown cancels the delay; the loop exits on the next iteration.
        }
    }

    [LoggerMessage(EventId = 5143, Level = LogLevel.Information, Message = "SEAP ingestion run worker processed {Processed} run(s).")]
    private static partial void LogSeapRunBatchProcessed(ILogger logger, int processed);
    [LoggerMessage(EventId = 5144, Level = LogLevel.Error, Message = "SEAP ingestion run worker tick failed; will retry.")]
    private static partial void LogSeapRunBatchFailed(ILogger logger, Exception exception);
}
