namespace Marketplace.Infrastructure.Resilience;

/// <summary>
/// Consecutive-failure circuit breaker. After <see cref="failureThreshold"/> consecutive failures the
/// circuit opens for <see cref="openDuration"/> (default one hour) and callers fail fast while it is open.
/// One success resets the counter. State is in-memory and per-process (resets on restart).
/// </summary>
public sealed class CircuitBreaker
{
    private readonly TimeProvider timeProvider;
    private readonly int failureThreshold;
    private readonly TimeSpan openDuration;
    private readonly object gate = new();
    private int consecutiveFailures;
    private DateTimeOffset openedAt;

    public CircuitBreaker(TimeProvider timeProvider, int failureThreshold = 3, TimeSpan? openDuration = null)
    {
        this.timeProvider = timeProvider;
        this.failureThreshold = Math.Max(1, failureThreshold);
        this.openDuration = openDuration ?? TimeSpan.FromHours(1);
    }

    public bool IsOpen
    {
        get
        {
            lock (gate)
            {
                if (consecutiveFailures < failureThreshold) return false;
                return timeProvider.GetUtcNow() - openedAt < openDuration;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (gate)
        {
            consecutiveFailures = 0;
        }
    }

    public void RecordFailure()
    {
        lock (gate)
        {
            consecutiveFailures++;
            if (consecutiveFailures >= failureThreshold)
                openedAt = timeProvider.GetUtcNow();
        }
    }
}
