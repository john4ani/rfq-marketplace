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
    /// exposed to end users and no Stripe charge is created. The optional <paramref name="progressRequestId"/>
    /// lets the caller poll real per-stage progress (Generating → Finalizing → Completed/Failed).</summary>
    Task<BidGenerationResult> GenerateForTenderAsync(Guid tenderId, CancellationToken cancellationToken, Guid? progressRequestId = null);
    /// <summary>
    /// Lists SEAP tenders, optionally filtered by the CPV prefixes of the selected activity categories and
    /// by <paramref name="radiusKm"/> measured from <paramref name="supplierCounty"/> (county-seat distance).
    /// The AI-mapped CategoryId is authoritative; the CPV-prefix match is the fallback for uncategorized tenders.
    /// <paramref name="cpvPrefixes"/> additionally restricts to tenders whose own CPV codes start with any
    /// prefix (deterministic, applies to categorized and uncategorized tenders alike), and
    /// <paramref name="counties"/> restricts to tenders whose county (normalized to a canonical name) is in
    /// the set. Both are applied only when non-empty.
    /// </summary>
    Task<IReadOnlyCollection<SeapTender>> ListTendersAsync(
        Guid[]? categoryIds, int? radiusKm, string? supplierCounty, CancellationToken cancellationToken,
        string[]? cpvPrefixes = null, string[]? counties = null);
    Task<SeapTender?> GetTenderAsync(Guid tenderId, CancellationToken cancellationToken);
}
