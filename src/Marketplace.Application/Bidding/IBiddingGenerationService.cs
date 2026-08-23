using Marketplace.Domain.Seap;

namespace Marketplace.Application.Bidding;

/// <summary>Outcome of an auto-bid generation run.</summary>
public sealed record BidGenerationResult(
    Guid TenderId,
    string ProposalTitle,
    string TechnicalProposalUrl,
    string BillOfQuantitiesUrl,
    bool ShadowMode,
    DateTimeOffset GeneratedAt);

public interface IBiddingGenerationService
{
    /// <summary>Runs RAG + LLM + document generation for one SEAP tender. In Shadow Mode nothing is
    /// exposed to end users and no Stripe charge is created.</summary>
    Task<BidGenerationResult> GenerateForTenderAsync(Guid tenderId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SeapTender>> ListTendersAsync(CancellationToken cancellationToken);
    Task<SeapTender?> GetTenderAsync(Guid tenderId, CancellationToken cancellationToken);
}
