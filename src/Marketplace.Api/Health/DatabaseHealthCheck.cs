using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Marketplace.Api.Health;

public sealed partial class DatabaseHealthCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            MarketplaceDbContext database = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();

            bool canConnect = await database.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception exception)
        {
            LogReadinessFailure(logger, exception);
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "PostgreSQL readiness check failed.")]
    private static partial void LogReadinessFailure(ILogger logger, Exception exception);
}
