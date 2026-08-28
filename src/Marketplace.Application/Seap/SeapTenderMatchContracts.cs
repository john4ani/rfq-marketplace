using Marketplace.Application.AiIntake;

namespace Marketplace.Application.Seap;

/// <summary>A single clarification exchange in the SEAP Tender Match chat.</summary>
public sealed record SeapClarificationTurn(string Question, string Answer);

public sealed record SeapTenderMatchRequest(
    string InitialInput,
    IReadOnlyCollection<SeapClarificationTurn> Turns);

/// <summary>
/// Result of the SEAP Tender Match interpreter: either a single clarification question (one at a time) or
/// the rigid database parameters extracted from the natural-language description. County codes are returned
/// both as the extracted codes (e.g. "IS") and as the canonical county names the database filters by
/// (e.g. "Iași") — the names are derived deterministically, never invented by the LLM.
/// </summary>
public sealed record SeapTenderMatchResult(
    bool ClarificationNeeded,
    string? ClarificationQuestion,
    IReadOnlyCollection<string> ExtractedCpvPrefixes,
    IReadOnlyCollection<string> ExtractedCountyCodes,
    IReadOnlyCollection<string> ExtractedCounties,
    string? SummaryText,
    string Provider,
    string Model,
    string PromptVersion,
    int InputTokens,
    int OutputTokens);

/// <summary>
/// SEAP Tender Match interpreter: a third distinct interpretation workflow (like General and Construction)
/// that translates a free-text description into rigid SEAP filter parameters — CPV prefixes and counties.
/// It reuses the shared OpenAI Responses transport and structured output, never the general prompt/schema.
/// </summary>
public interface ISeapTenderMatchInterpreter
{
    bool IsAvailable { get; }
    Task<SeapTenderMatchResult> InterpretAsync(SeapTenderMatchRequest request, CancellationToken cancellationToken);
}

/// <summary>The filter profile persisted on the represented company (<c>SupplierProfile</c>).</summary>
public sealed record SeapTenderMatchProfile(
    Guid CompanyId,
    IReadOnlyCollection<string> CpvPrefixes,
    IReadOnlyCollection<string> Counties);

public interface ISeapTenderMatchService
{
    AssistedIntakeAvailability Availability { get; }
    Task<SeapTenderMatchResult> InterpretAsync(
        string input, IReadOnlyCollection<SeapClarificationTurn> turns, CancellationToken cancellationToken);
    Task<SeapTenderMatchProfile> SaveFiltersAsync(
        Guid companyId, IReadOnlyCollection<string> cpvPrefixes, IReadOnlyCollection<string> counties,
        CancellationToken cancellationToken);
}
