using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.AiIntake;

public enum AssistedIntakeMode { OpenAi }
public enum ClarificationField { Category, Quantity, Specification, ProjectScope, DeliveryAddress, DeliveryDate }

public sealed record AssistedRequirementLine(
    RequirementKind Kind,
    string Name,
    string? Specification,
    decimal? Quantity,
    UnitOfMeasure? Unit,
    bool AlternativesAccepted,
    Guid? CategoryId);

public sealed record AssistedNeedDraft(
    ProcurementInputType InputType,
    string Title,
    string? ProjectName,
    string? Description,
    Guid? PrimaryCategoryId,
    string DeliveryAddress,
    string DeliveryLocality,
    string County,
    int SupplierSearchRadiusKm,
    DateOnly? PreferredDeliveryDate,
    DateTimeOffset QuotationDeadline,
    bool AlternativeProductsAccepted,
    bool AllowSplit,
    bool DeliveryRequired,
    decimal? EstimatedBudget,
    string? BuyerNotes,
    IReadOnlyCollection<AssistedRequirementLine> Lines);

public sealed record AssistedClarification(
    ClarificationField Field,
    string Question,
    IReadOnlyCollection<string> Choices);

public sealed record AssistedIntakeAnswer(ClarificationField Field, string Question, string Answer);

public sealed record AssistedResearchSource(string Title, string Url, string Finding);

public sealed record AssistedIntakeResult(
    Guid SessionId,
    string InitialInput,
    AiIntakeSessionStatus Status,
    AssistedIntakeMode Mode,
    string Provider,
    string Model,
    string PromptVersion,
    AssistedNeedDraft Draft,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> Uncertainties,
    IReadOnlyCollection<AssistedResearchSource> ResearchSources,
    AssistedClarification? Clarification,
    IReadOnlyCollection<AssistedIntakeAnswer> Answers,
    int InputTokens,
    int OutputTokens,
    Guid? ApprovedRfqId,
    SuggestedProcurementMode? SuggestedMode = null);

public sealed record AssistedIntakeAvailability(bool Enabled, bool ModelAvailable);

public interface IAssistedIntakeService
{
    AssistedIntakeAvailability Availability { get; }
    Task<AssistedIntakeResult> StartAsync(Guid buyerId, string input, CancellationToken cancellationToken);
    Task<AssistedIntakeResult?> GetAsync(Guid buyerId, Guid sessionId, CancellationToken cancellationToken);
    Task<AssistedIntakeResult?> ClarifyAsync(Guid buyerId, Guid sessionId, string answer, CancellationToken cancellationToken);
    Task<Rfq?> ApproveAsync(Guid buyerId, Guid sessionId, RfqDraft approvedDraft,
        bool completedInAdvancedForm, CancellationToken cancellationToken);
}

public sealed class AiIntakeConcurrencyException(string message, Exception innerException) : Exception(message, innerException);
public sealed class AiIntakeUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
