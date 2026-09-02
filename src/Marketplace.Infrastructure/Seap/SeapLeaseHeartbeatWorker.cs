using Marketplace.Application.Seap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// Renews the lease of every run claimed by THIS worker instance while the ingestion worker is busy with long
// document downloads / OCR / LLM calls. Each tick uses a fresh DI scope/DbContext (never shared with the
// processing pipeline), so the heartbeat cannot interfere with a checkpoint transaction. A run whose owner
// died is not renewed and becomes reclaimable when its lease expires. Gated by the same
// Features:SeapIngestionEnabled switch as the other SEAP workers.
public sealed partial class SeapLeaseHeartbeatWorker(
    IServiceScopeFactory scopeFactory,
    SeapWorkerIdentity workerIdentity,
    IOptionsMonitor<FeaturesOptions> features,
    IOptionsMonitor<SeapOptions> seapOptions,
    ILogger<SeapLeaseHeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSeconds = seapOptions.CurrentValue.RunHeartbeatIntervalSeconds;
            if (!features.CurrentValue.SeapIngestionWorkerEnabled || intervalSeconds <= 0)
            {
                await Delay(TimeSpan.FromSeconds(60), stoppingToken);
                continue;
            }

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                ISeapIngestionService service = scope.ServiceProvider.GetRequiredService<ISeapIngestionService>();
                int renewed = await service.RenewActiveLeasesAsync(workerIdentity.Id, stoppingToken);
                if (renewed > 0)
                    LogSeapLeasesRenewed(logger, renewed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSeapLeaseRenewalFailed(logger, exception);
            }

            await Delay(TimeSpan.FromSeconds(Math.Max(5, intervalSeconds)), stoppingToken);
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

    [LoggerMessage(EventId = 5146, Level = LogLevel.Debug, Message = "SEAP lease heartbeat renewed {Renewed} run(s).")]
    private static partial void LogSeapLeasesRenewed(ILogger logger, int renewed);
    [LoggerMessage(EventId = 5147, Level = LogLevel.Error, Message = "SEAP lease heartbeat tick failed; will retry.")]
    private static partial void LogSeapLeaseRenewalFailed(ILogger logger, Exception exception);
}
