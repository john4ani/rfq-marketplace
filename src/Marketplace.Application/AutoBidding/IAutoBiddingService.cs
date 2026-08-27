namespace Marketplace.Application.AutoBidding;

/// <summary>Outcome of the specification (Caiet de sarcini) auto-bidding pipeline.</summary>
public sealed record SpecificationBidResult(
    Guid DocumentId,
    string FileName,
    string DocumentUrl,
    string Markdown,
    string ProposalTitle,
    string TechnicalProposalUrl,
    string BillOfQuantitiesUrl,
    bool ShadowMode,
    DateTimeOffset GeneratedAt);

/// <summary>
/// Auto-bidding pipeline for an uploaded technical specification (Caiet de sarcini) PDF:
/// save to object storage → extract text/tables as Markdown via Azure Document Intelligence →
/// persist the Markdown → generate a basic technical proposal through the configured LLM provider.
/// </summary>
public interface IAutoBiddingService
{
    /// <summary>Uploads a specification PDF and runs the full pipeline (storage → Markdown → LLM proposal).</summary>
    Task<SpecificationBidResult> ProcessSpecificationAsync(
        Guid companyId, string fileName, Stream content, Guid actorId, CancellationToken cancellationToken,
        Guid? progressRequestId = null);

    /// <summary>Re-runs the LLM proposal step for an already-extracted specification document.</summary>
    Task<SpecificationBidResult> GenerateFromSpecificationAsync(
        Guid companyId, Guid documentId, CancellationToken cancellationToken, Guid? progressRequestId = null);
}
