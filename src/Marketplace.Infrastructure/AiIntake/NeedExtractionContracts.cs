using Marketplace.Application.AiIntake;

namespace Marketplace.Infrastructure.AiIntake;

internal sealed record IntakeCategory(Guid Id, string Name, string Slug);
internal sealed record ClarificationTurn(ClarificationField Field, string Question, string Answer);

internal sealed record NeedExtractionContext(
    Guid BuyerId,
    string InitialInput,
    string BuyerCity,
    string BuyerCounty,
    int BuyerRadiusKm,
    IReadOnlyCollection<IntakeCategory> Categories,
    IReadOnlyCollection<ClarificationTurn> Turns,
    AssistedNeedDraft? PreviousDraft,
    DateTimeOffset Now);

internal sealed record NeedExtractionOutcome(
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

internal interface INeedExtractionProvider
{
    bool IsAvailable { get; }
    Task<NeedExtractionOutcome> ExtractAsync(NeedExtractionContext context, CancellationToken cancellationToken);
}
