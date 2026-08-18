using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.AiIntake;

public sealed record IntakeCategory(Guid Id, string Name, string Slug);
public sealed record ClarificationTurn(ClarificationField Field, string Question, string Answer);

public sealed record GeneralNeedInterpretationRequest(
    Guid BuyerId,
    string InitialInput,
    string BuyerCity,
    string BuyerCounty,
    int BuyerRadiusKm,
    IReadOnlyCollection<IntakeCategory> Categories,
    IReadOnlyCollection<ClarificationTurn> Turns,
    AssistedNeedDraft? PreviousDraft,
    DateTimeOffset Now);

public sealed record GeneralNeedInterpretation(
    AssistedIntakeMode Mode,
    string Provider,
    string Model,
    string PromptVersion,
    AssistedNeedDraft Draft,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> Uncertainties,
    IReadOnlyCollection<AssistedResearchSource> ResearchSources,
    AssistedClarification? Clarification,
    int InputTokens,
    int OutputTokens,
    SuggestedProcurementMode? SuggestedMode = null);

// A non-binding hint from the general interpreter. The caller keeps the current
// flow until the user explicitly chooses the specialised Construction experience.
public enum SuggestedProcurementMode { GeneralProcurement, ConstructionProjects }

public interface IGeneralNeedInterpreter
{
    bool IsAvailable { get; }
    Task<GeneralNeedInterpretation> InterpretAsync(GeneralNeedInterpretationRequest request, CancellationToken cancellationToken);
}

// Construction Projects intake: a distinct interpretation workflow (its own prompt, schema and clarification
// semantics) that understands a construction PROJECT first and then derives the shared Procurement Need
// representation. It must not share one universal workflow with the general interpreter, but it reuses the same
// OpenAI transport and the same AssistedNeedDraft shape so the derived need flows through the shared Procurement Core.
public sealed record ConstructionProjectInterpretationRequest(
    Guid UserId,
    string InitialInput,
    string BuyerCity,
    string BuyerCounty,
    int BuyerRadiusKm,
    IReadOnlyCollection<IntakeCategory> Categories,
    IReadOnlyCollection<ClarificationTurn> Turns,
    AssistedNeedDraft? PreviousDraft,
    DateTimeOffset Now);

public sealed record ConstructionProjectInterpretation(
    string ProjectSummary,
    AssistedNeedDraft Draft,
    IReadOnlyCollection<string> Assumptions,
    IReadOnlyCollection<string> Uncertainties,
    AssistedClarification? Clarification,
    string Provider,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens);

public interface IConstructionProjectInterpreter
{
    bool IsAvailable { get; }
    Task<ConstructionProjectInterpretation> InterpretAsync(
        ConstructionProjectInterpretationRequest request, CancellationToken cancellationToken);
}
