namespace Marketplace.Domain.SupplierDiscovery;

public enum SupplierWebDiscoveryRunStatus
{
    EvidenceReady,
    Qualified,
    Failed
}

// Stores only the commercial evidence needed for an Administrator retry/review.
// It never represents a Company, SupplierCandidate, contact permission or verification.
public sealed class SupplierWebDiscoveryRun
{
    private SupplierWebDiscoveryRun() { }

    public Guid Id { get; private set; }
    // A run may originate from a real procurement need or from the Admin company
    // registry. Only a Need-context run may create an OpportunityMatch.
    public Guid? RfqId { get; private set; }
    public Guid StartedBy { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public SupplierWebDiscoveryRunStatus Status { get; private set; }
    public string EvidenceJson { get; private set; } = "{}";
    public string? QualificationJson { get; private set; }
    public string? FailureMessage { get; private set; }
    public int WebSearchCallCount { get; private set; }
    public long? DiscoveryLatencyMs { get; private set; }
    public int? DiscoveryInputTokens { get; private set; }
    public int? DiscoveryOutputTokens { get; private set; }
    public long? QualificationLatencyMs { get; private set; }
    public int? QualificationInputTokens { get; private set; }
    public int? QualificationOutputTokens { get; private set; }

    public static SupplierWebDiscoveryRun Start(Guid? rfqId, Guid administratorId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), RfqId = rfqId, StartedBy = administratorId, StartedAt = now,
        Status = SupplierWebDiscoveryRunStatus.Failed, FailureMessage = "Căutarea nu a fost finalizată."
    };

    public void RecordEvidence(string evidenceJson, int webSearchCallCount, long latencyMs, int inputTokens, int outputTokens, DateTimeOffset now)
    {
        EvidenceJson = evidenceJson;
        WebSearchCallCount = webSearchCallCount;
        DiscoveryLatencyMs = latencyMs;
        DiscoveryInputTokens = inputTokens;
        DiscoveryOutputTokens = outputTokens;
        Status = SupplierWebDiscoveryRunStatus.EvidenceReady;
        FailureMessage = null;
        CompletedAt = now;
    }

    public void RecordQualification(string qualificationJson, long latencyMs, int inputTokens, int outputTokens, DateTimeOffset now)
    {
        QualificationJson = qualificationJson;
        QualificationLatencyMs = latencyMs;
        QualificationInputTokens = inputTokens;
        QualificationOutputTokens = outputTokens;
        Status = SupplierWebDiscoveryRunStatus.Qualified;
        FailureMessage = null;
        CompletedAt = now;
    }

    public void RecordQualificationFailure(string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(EvidenceJson) || EvidenceJson == "{}") throw new InvalidOperationException("Web evidence must exist before qualification can be retried.");
        Status = SupplierWebDiscoveryRunStatus.EvidenceReady;
        FailureMessage = message;
        CompletedAt = now;
    }

    public void RecordFailure(string message, DateTimeOffset now) { Status = SupplierWebDiscoveryRunStatus.Failed; FailureMessage = message; CompletedAt = now; }
}
