using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.AiIntake;

// Result of the distinct Construction intake workflow. It reuses the shared need/clarification shapes so the derived
// need flows through the shared Procurement Core, and adds the human-readable project understanding on top.
public sealed record ConstructionIntakeResult(
    Guid SessionId,
    string InitialInput,
    AiIntakeSessionStatus Status,
    string Provider,
    string Model,
    string PromptVersion,
    string ProjectSummary,
    AssistedNeedDraft Draft,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> Uncertainties,
    AssistedClarification? Clarification,
    IReadOnlyCollection<AssistedIntakeAnswer> Answers,
    int InputTokens,
    int OutputTokens,
    Guid? ApprovedRfqId);

public interface IConstructionIntakeService
{
    AssistedIntakeAvailability Availability { get; }
    Task<ConstructionIntakeResult> StartAsync(Guid userId, string input, CancellationToken cancellationToken);
    Task<ConstructionIntakeResult?> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task<ConstructionIntakeResult?> ClarifyAsync(Guid userId, Guid sessionId, string answer, CancellationToken cancellationToken);
    Task<Rfq?> ApproveAsync(Guid userId, Guid sessionId, RfqDraft approvedDraft,
        bool completedInAdvancedForm, CancellationToken cancellationToken);
}
