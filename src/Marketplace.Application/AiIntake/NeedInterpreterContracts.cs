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
    int OutputTokens);

public interface IGeneralNeedInterpreter
{
    bool IsAvailable { get; }
    Task<GeneralNeedInterpretation> InterpretAsync(GeneralNeedInterpretationRequest request, CancellationToken cancellationToken);
}

// A routing boundary only until Task 9 owns a ConstructionProject aggregate and specialised facts.
public sealed record ConstructionProjectInterpretationRequest(
    Guid UserId,
    string Description,
    IReadOnlyCollection<IntakeCategory> Categories,
    DateTimeOffset Now);

public sealed record ConstructionProjectInterpretation(
    string Summary,
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
