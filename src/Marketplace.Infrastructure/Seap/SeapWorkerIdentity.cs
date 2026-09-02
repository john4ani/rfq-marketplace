namespace Marketplace.Infrastructure.Seap;

/// <summary>Process-wide identity shared by the ingestion-run worker and the lease-heartbeat worker of this
/// instance: claims are attributed to this id and heartbeats renew exactly the runs this instance claimed.</summary>
public sealed class SeapWorkerIdentity
{
    public SeapWorkerIdentity()
    {
        Id = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    public string Id { get; }
}
