using System.Collections.Concurrent;

namespace Marketplace.Infrastructure.Seap;

/// <summary>Snapshot of a run whose processing is currently in flight in this process.</summary>
public sealed record SeapActiveRunEntry(Guid RunId, string WorkerId, long ClaimVersion, DateTimeOffset LastSeenAt);

/// <summary>
/// In-memory registry of the runs this PROCESS is actually processing. A run is registered after a successful
/// claim and removed when its processing ends (any path — success, failure, lease lost, host shutdown). The
/// lease-heartbeat worker renews ONLY runs present in this registry (with a fresh <see cref="LastSeenAt"/>),
/// so a processing task that died without a terminal state can never be kept alive by a process-wide heartbeat —
/// once the run disappears from the registry (or its touch loop stops), its lease expires and another worker
/// can reclaim it. Thread-safe; process-wide by design.
/// </summary>
public sealed class SeapActiveRunRegistry
{
    private sealed class Entry(string workerId, long claimVersion)
    {
        public string WorkerId { get; } = workerId;
        public long ClaimVersion { get; } = claimVersion;
        public DateTimeOffset LastSeenAt { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, Entry> _runs = new();

    public void Add(Guid runId, string workerId, long claimVersion, DateTimeOffset now)
        => _runs[runId] = new Entry(workerId, claimVersion) { LastSeenAt = now };

    /// <summary>Refreshes the last-seen timestamp of the run (called by the run's own processing touch loop);
    /// returns false when the run is no longer registered (lease already lost/removed).</summary>
    public bool Touch(Guid runId, DateTimeOffset now)
    {
        if (!_runs.TryGetValue(runId, out Entry? entry)) return false;
        entry.LastSeenAt = now;
        return true;
    }

    /// <summary>Removes the run only when the caller still owns the claim (fencing token matches), so a stale
    /// worker cannot remove the entry of the worker that reclaimed the run.</summary>
    public bool Remove(Guid runId, long claimVersion)
    {
        if (!_runs.TryGetValue(runId, out Entry? entry) || entry.ClaimVersion != claimVersion) return false;
        return _runs.TryRemove(runId, out _);
    }

    public IReadOnlyCollection<SeapActiveRunEntry> GetActiveFor(string workerId)
    {
        var snapshot = new List<SeapActiveRunEntry>(_runs.Count);
        foreach ((Guid runId, Entry entry) in _runs)
        {
            if (string.Equals(entry.WorkerId, workerId, StringComparison.Ordinal))
                snapshot.Add(new SeapActiveRunEntry(runId, entry.WorkerId, entry.ClaimVersion, entry.LastSeenAt));
        }
        return snapshot;
    }
}
