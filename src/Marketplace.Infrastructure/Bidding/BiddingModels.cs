namespace Marketplace.Infrastructure.Bidding;

/// <summary>Parsed LLM output for one auto-bid (pure model, no I/O).</summary>
public sealed record BiddingProposal(string Title, IReadOnlyCollection<ProposalSection> Sections, IReadOnlyCollection<BoqLine> BillOfQuantities);

public sealed record ProposalSection(string Heading, string Content);

public sealed record BoqLine(int Position, string Description, string Unit, decimal Quantity, decimal UnitPrice, decimal Total);

/// <summary>A generated document ready for S3 upload.</summary>
public sealed record GeneratedDocument(byte[] Content, string FileName, string ContentType);

public interface IDocumentGenerator
{
    Task<GeneratedDocument> GenerateTechnicalProposalAsync(BiddingProposal proposal, CancellationToken cancellationToken);
    Task<GeneratedDocument> GenerateBillOfQuantitiesAsync(BiddingProposal proposal, CancellationToken cancellationToken);
}
