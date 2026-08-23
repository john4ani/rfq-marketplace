namespace Marketplace.Domain.Seap;

public enum SeapTenderStatus { Imported, ReadyForBidding, BidGenerated, Failed }

/// <summary>
/// A public tender notice imported from e-licitatie.ro. Kept as its own aggregate: a SEAP tender is a
/// public procurement opportunity, not a CompaSpot buyer need (the buying institution is external and
/// is not an ApplicationUser, so forcing tenders into Rfq would pollute the marketplace).
/// </summary>
public sealed class SeapTender
{
    private readonly List<SeapTenderAttachment> _attachments = [];
    private SeapTender() { }

    public Guid Id { get; private set; }
    public string SeapExternalId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IReadOnlyCollection<string> CpvCodes { get; private set; } = [];
    public decimal? EstimatedValue { get; private set; }
    public string? Currency { get; private set; }
    public string BuyerName { get; private set; } = string.Empty;
    public DateTimeOffset PublicationDate { get; private set; }
    public DateTimeOffset Deadline { get; private set; }
    public SeapTenderStatus Status { get; private set; }
    public string? ProposalTitle { get; private set; }
    public string? TechnicalProposalUrl { get; private set; }
    public string? BillOfQuantitiesUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<SeapTenderAttachment> Attachments => _attachments;

    public static SeapTender Import(
        string seapExternalId,
        string title,
        string? description,
        IReadOnlyCollection<string> cpvCodes,
        decimal? estimatedValue,
        string? currency,
        string buyerName,
        DateTimeOffset publicationDate,
        DateTimeOffset deadline,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            SeapExternalId = Required(seapExternalId, "Identificatorul SEAP este obligatoriu."),
            Title = Required(title, "Titlul anunțului este obligatoriu."),
            Description = NullIfBlank(description),
            CpvCodes = (cpvCodes ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            EstimatedValue = estimatedValue,
            Currency = NullIfBlank(currency),
            BuyerName = Required(buyerName, "Instituția contractantă este obligatorie."),
            PublicationDate = publicationDate,
            Deadline = deadline,
            Status = SeapTenderStatus.Imported,
            CreatedAt = now,
            UpdatedAt = now
        };

    public SeapTenderAttachment AddAttachment(string fileName, string s3Url, DateTimeOffset now)
    {
        SeapTenderAttachment attachment = SeapTenderAttachment.Create(Id, fileName, s3Url, now);
        _attachments.Add(attachment);
        return attachment;
    }

    public void MarkReadyForBidding(DateTimeOffset now)
    {
        Status = SeapTenderStatus.ReadyForBidding;
        UpdatedAt = now;
    }

    public void MarkBidGenerated(DateTimeOffset now)
    {
        Status = SeapTenderStatus.BidGenerated;
        UpdatedAt = now;
    }

    public void RecordGeneratedBid(string proposalTitle, string technicalProposalUrl, string billOfQuantitiesUrl, DateTimeOffset now)
    {
        ProposalTitle = proposalTitle;
        TechnicalProposalUrl = technicalProposalUrl;
        BillOfQuantitiesUrl = billOfQuantitiesUrl;
        MarkBidGenerated(now);
    }

    public void MarkFailed(string reason, DateTimeOffset now)
    {
        Status = SeapTenderStatus.Failed;
        UpdatedAt = now;
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
