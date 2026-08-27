using System.Collections.Concurrent;

namespace Marketplace.Infrastructure.AutoBidding;

/// <summary>Current stage of one running auto-bidding pipeline, keyed by a client-generated request id.</summary>
public enum PipelineStage
{
    Extracting,
    Generating,
    Finalizing,
    Completed,
    Failed
}

/// <summary>
/// In-memory pipeline progress registry (singleton). The frontend generates a request id, the pipeline
/// writes each stage as it advances, and a lightweight GET endpoint lets the UI show real per-stage
/// progress ("Etapa 1/3: se extrag tabelele…", "Etapa 2/3: Claude redactează…") instead of an endless
/// spinner. Entries are bounded and expire so an abandoned request can never grow the dictionary.
/// </summary>
public interface IPipelineProgressStore
{
    void Report(Guid requestId, PipelineStage stage, string? message = null);
    PipelineProgress? Find(Guid requestId);
}

public sealed record PipelineProgress(PipelineStage Stage, string? Message);

public sealed class PipelineProgressStore : IPipelineProgressStore
{
    private const int MaxEntries = 64;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<Guid, PipelineProgress> _entries = new();

    public void Report(Guid requestId, PipelineStage stage, string? message = null)
    {
        _entries[requestId] = new PipelineProgress(stage, message);
        if (_entries.Count > MaxEntries)
        {
            // Best-effort bound: drop the oldest entries when the registry grows past its cap.
            foreach (Guid key in _entries.Keys.Take(_entries.Count - MaxEntries))
                _entries.TryRemove(key, out _);
        }
    }

    public PipelineProgress? Find(Guid requestId)
    {
        if (_entries.TryGetValue(requestId, out PipelineProgress? progress) && progress is not null)
            return progress;
        return null;
    }
}
