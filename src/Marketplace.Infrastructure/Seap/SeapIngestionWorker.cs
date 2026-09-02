using Marketplace.Application.Seap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// Background worker: gated by the hot-reloadable Features:SeapIngestionEnabled switch. While disabled it
// simply re-checks every minute, so toggling the flag takes effect without a redeploy. When enabled it
// polls the public e-licitatie.ro notice search and imports target notices through the staging table.
public sealed partial class SeapIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<FeaturesOptions> features,
    IOptionsMonitor<SeapOptions> seapOptions,
    ILogger<SeapIngestionWorker> logger) : BackgroundService
{
    private bool? lastEnabledState;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            bool enabled = features.CurrentValue.SeapIngestionWorkerEnabled;
            if (!enabled)
            {
                if (lastEnabledState != false)
                {
                    LogSeapDisabled(logger);
                    lastEnabledState = false;
                }
                await Delay(TimeSpan.FromSeconds(60), stoppingToken);
                continue;
            }
            lastEnabledState = true;
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                ISeapIngestionService service = scope.ServiceProvider.GetRequiredService<ISeapIngestionService>();
                int imported = await service.PollAndImportAsync(stoppingToken);
                LogSeapPollCompleted(logger, imported);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSeapPollFailed(logger, exception);
            }
            await Delay(TimeSpan.FromSeconds(Math.Max(60, seapOptions.CurrentValue.PollingIntervalSeconds)), stoppingToken);
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

    [LoggerMessage(EventId = 5106, Level = LogLevel.Information, Message = "SEAP ingestion is disabled by the Features:SeapIngestionWorkerEnabled switch.")]
    private static partial void LogSeapDisabled(ILogger logger);
    [LoggerMessage(EventId = 5107, Level = LogLevel.Information, Message = "SEAP poll completed; {Imported} tenders imported.")]
    private static partial void LogSeapPollCompleted(ILogger logger, int imported);
    [LoggerMessage(EventId = 5108, Level = LogLevel.Error, Message = "SEAP poll failed; will retry.")]
    private static partial void LogSeapPollFailed(ILogger logger, Exception exception);
}
