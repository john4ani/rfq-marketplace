using Marketplace.Domain.Suppliers;

namespace Marketplace.Domain.Seap;

public enum SeapTenderStatus { Imported, ReadyForBidding, BidGenerated, Failed }

/// <summary>
/// Structured values extracted from a notice by the AI summarizer (see <c>ISeapSummaryGenerator</c>):
/// decision-critical facts for a supplier's participation decision. Any field may be null when the notice
/// does not state it — the LLM must never invent data. <see cref="SummaryPoints"/> holds the short
/// „Sinteză AI" bullets shown on the tender detail page.
/// </summary>
public sealed record SeapAiExtraction(
    string? Description,
    string? AwardCriterion,
    string? ParticipationGuarantee,
    string? FundingSource,
    string? ContractDuration,
    string? QualificationRequirements,
    string? KeyPersonnel,
    IReadOnlyCollection<string> SummaryPoints);

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
    /// <summary>Numeric id of the notice in SEAP (carried by the search payload). Persisted so the published
    /// document list can be resolved again on demand (the notice-search payload does not carry documents).</summary>
    public int? NoticeId { get; private set; }
    /// <summary>Notice type in SEAP (2 = CN, 17 = SCN); required alongside <see cref="NoticeId"/> to resolve
    /// the published documents.</summary>
    public int? SysNoticeTypeId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IReadOnlyCollection<string> CpvCodes { get; private set; } = [];
    /// <summary>Human-readable CPV labels (e.g. "Montare de garduri"), aligned positionally with
    /// <see cref="CpvCodes"/>. The AI categorizer maps them to CompaSpot categories.</summary>
    public IReadOnlyCollection<string> CpvNames { get; private set; } = [];
    public decimal? EstimatedValue { get; private set; }
    public string? Currency { get; private set; }
    public string BuyerName { get; private set; } = string.Empty;
    /// <summary>County of the contracting authority, derived deterministically from the buyer name
    /// (e.g. "DIRECTIA ... VASLUI" → "Vaslui"). Null when the name carries no recognizable county.</summary>
    public string? County { get; private set; }
    /// <summary>CompaSpot category resolved by the AI categorizer from the CPV label. Null until the
    /// tender is categorized; the CPV-prefix filter acts as fallback for uncategorized tenders.</summary>
    public Guid? CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public DateTimeOffset PublicationDate { get; private set; }
    public DateTimeOffset Deadline { get; private set; }
    public SeapTenderStatus Status { get; private set; }
    public string? ProposalTitle { get; private set; }
    public string? TechnicalProposalUrl { get; private set; }
    public string? BillOfQuantitiesUrl { get; private set; }
    /// <summary>Award criterion stated in the notice (e.g. „Prețul cel mai scăzut" or „Cel mai bun raport
    /// calitate-preț" with weights), extracted by the AI summarizer at import.</summary>
    public string? AwardCriterion { get; private set; }
    /// <summary>Participation guarantee amount + currency (e.g. „120.000 RON"), when the notice states one.</summary>
    public string? ParticipationGuarantee { get; private set; }
    /// <summary>Funding source (e.g. PNRR, fonduri europene, buget local), when specified.</summary>
    public string? FundingSource { get; private set; }
    /// <summary>Contract duration (e.g. „12 luni", „60 de zile"), when specified.</summary>
    public string? ContractDuration { get; private set; }
    /// <summary>Summary of the technical/professional capacity requirements (similar experience, turnover).</summary>
    public string? QualificationRequirements { get; private set; }
    /// <summary>Short list of the key experts requested (e.g. „Manager de proiect, Inginer RTE").</summary>
    public string? KeyPersonnel { get; private set; }
    /// <summary>AI-synthesis bullets for the „Sinteză AI" tab (max 5, short). Empty until generated — an empty
    /// array means „processing/not yet generated", never a failed state for the tender.</summary>
    public IReadOnlyCollection<string> SummaryPoints { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<SeapTenderAttachment> Attachments => _attachments;

    public static SeapTender Import(
        string seapExternalId,
        string title,
        string? description,
        IReadOnlyCollection<string> cpvCodes,
        IReadOnlyCollection<string> cpvNames,
        decimal? estimatedValue,
        string? currency,
        string buyerName,
        string? county,
        DateTimeOffset publicationDate,
        DateTimeOffset deadline,
        DateTimeOffset now,
        int? noticeId = null,
        int? sysNoticeTypeId = null) => new()
        {
            Id = Guid.NewGuid(),
            SeapExternalId = Required(seapExternalId, "Identificatorul SEAP este obligatoriu."),
            NoticeId = noticeId,
            SysNoticeTypeId = sysNoticeTypeId,
            Title = Required(title, "Titlul anunțului este obligatoriu."),
            Description = NullIfBlank(description),
            CpvCodes = (cpvCodes ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            CpvNames = (cpvNames ?? []).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray(),
            EstimatedValue = estimatedValue,
            Currency = NullIfBlank(currency),
            BuyerName = Required(buyerName, "Instituția contractantă este obligatorie."),
            County = NullIfBlank(county),
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

    /// <summary>Assigns the CompaSpot category resolved by the AI categorizer (CPV label → category).</summary>
    public void Categorize(Guid categoryId, DateTimeOffset now)
    {
        CategoryId = categoryId;
        UpdatedAt = now;
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

    /// <summary>Backfills the deterministic county derived from the contracting-authority name for tenders
    /// imported before the county enrichment existed. Idempotent: skips tenders that already have a county
    /// and values that carry no recognizable county.</summary>
    public void BackfillCounty(string? county, DateTimeOffset now)
    {
        if (County is not null || string.IsNullOrWhiteSpace(county)) return;
        County = county.Trim();
        UpdatedAt = now;
    }

    /// <summary>Records the numeric SEAP notice id/type for tenders imported before the document-resolution
    /// feature existed, so their published documents can be resolved on demand. Idempotent: never overwrites
    /// an already-recorded id and ignores null input.</summary>
    public void BackfillNoticeSource(int? noticeId, int? sysNoticeTypeId, DateTimeOffset now)
    {
        if (NoticeId is not null || noticeId is null) return;
        NoticeId = noticeId;
        SysNoticeTypeId = sysNoticeTypeId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Persists the AI extraction produced at import („Sinteză AI" bullets + award criterion, participation
    /// guarantee, funding source, duration, qualification requirements, key personnel). Enrichment is never a
    /// gate: the tender is fully usable without it. The AI's 2–3 sentence acquisition summary fills
    /// <see cref="Description"/> only when the notice's own full text is absent — a parser-delivered full
    /// text always wins over the AI summary.
    /// </summary>
    public void ApplyAiExtraction(SeapAiExtraction extraction, DateTimeOffset now)
    {
        if (Description is null && !string.IsNullOrWhiteSpace(extraction.Description))
            Description = extraction.Description.Trim();
        AwardCriterion = NullIfBlank(extraction.AwardCriterion);
        ParticipationGuarantee = NullIfBlank(extraction.ParticipationGuarantee);
        FundingSource = NullIfBlank(extraction.FundingSource);
        ContractDuration = NullIfBlank(extraction.ContractDuration);
        QualificationRequirements = NullIfBlank(extraction.QualificationRequirements);
        KeyPersonnel = NullIfBlank(extraction.KeyPersonnel);
        if (extraction.SummaryPoints is { Count: > 0 })
            SummaryPoints = extraction.SummaryPoints
                .Select(point => point.Trim())
                .Where(point => point.Length > 0)
                .Take(5)
                .ToArray();
        UpdatedAt = now;
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
