using Marketplace.Domain.Seap;

namespace Marketplace.Application.Seap;

/// <summary>Raw tender notice shape returned by the public e-licitatie.ro JSON API.</summary>
public sealed record SeapTenderNoticeDto(
    string Id,
    string Title,
    string? Description,
    IReadOnlyCollection<string> CpvCodes,
    decimal? EstimatedValue,
    string? Currency,
    string BuyerName,
    DateTimeOffset PublicationDate,
    DateTimeOffset Deadline,
    IReadOnlyCollection<SeapAttachmentDto> Attachments,
    /// <summary>Human-readable CPV labels (e.g. "Montare de garduri"), aligned with <see cref="CpvCodes"/>.</summary>
    IReadOnlyCollection<string>? CpvNames = null,
    /// <summary>County derived from <see cref="BuyerName"/> (e.g. "Vaslui"); null when unrecognizable.</summary>
    string? County = null);

public sealed record SeapAttachmentDto(string FileName, string DownloadUrl);

public sealed record SeapTenderImportResult(SeapTender? Tender, int DownloadedAttachmentCount, bool Duplicate);

/// <summary>Envelope returned by the real public e-licitatie.ro notice search (GetCNoticeList).</summary>
public sealed record SeapNoticeSearchResponse(
    int Total,
    IReadOnlyCollection<SeapNoticeSearchItem> Items,
    bool SearchTooLong);

/// <summary>Projection of a real e-licitatie.ro search item; only the fields the importer needs. All
/// optional fields may be missing or null in real payloads — the parser normalizes them to empty/null
/// instead of throwing NullReferenceException.</summary>
public sealed record SeapNoticeSearchItem(
    string? NoticeNo,
    string? ContractTitle,
    string? ContractingAuthorityNameAndFN,
    string? CpvCodeAndName,
    decimal? EstimatedValueRon,
    DateTimeOffset NoticeStateDate,
    DateTimeOffset? MinTenderReceiptDeadline,
    DateTimeOffset? MaxTenderReceiptDeadline);

public interface ISeapApiClient
{
    Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken);

    /// <summary>Fetches one page of notices from the live public SEAP search, optionally restricted to a
    /// publication-date window. Live-only: the on-demand „Caută în SEAP" search never falls back to the
    /// local golden payloads. Returns an empty collection on any failure.</summary>
    Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken);
}

public interface ISeapAttachmentStorage
{
    Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken);
}

public interface ISeapIngestionService
{
    Task<SeapTenderImportResult> ImportNoticeAsync(SeapTenderNoticeDto notice, CancellationToken cancellationToken);
    Task<int> PollAndImportAsync(CancellationToken cancellationToken);

    /// <summary>
    /// On-demand SEAP search (Anunțuri SEAP → „Caută în SEAP"): takes the active filter parameters
    /// (CPV prefixes + counties), pages the live e-licitatie.ro search — optionally restricted to a
    /// publication-date window (default: the current year) — derives each notice's county deterministically,
    /// imports the notices matching the parameters (no LLM categorization, no attachment downloads), and
    /// returns them. Because the anonymous search caps wide windows at ~3000 notices with an unstable slice,
    /// it additionally scans the trailing 30 days (clamped to the requested window) so recent matching
    /// notices are found reliably. Bypasses the background hourly import cap because it is a user-initiated action.
    /// </summary>
    Task<IReadOnlyCollection<SeapTender>> SearchAndImportAsync(
        string[]? cpvPrefixes, string[]? counties, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of the AI categorizer for a standardized CPV code.</summary>
public sealed record SeapCategorizationResult(Guid CategoryId, string CategoryName, bool NewCategory, bool FromCache);

/// <summary>
/// Resolves the CompaSpot category for a SEAP CPV code. CPV codes are standardized, so the persistent
/// <c>CpvCategoryMappings</c> cache is consulted first and the LLM is called at most once per unique code;
/// categories the LLM returns that do not exist yet are auto-created under the General Procurement root.
/// Every failure degrades to null — the tender still imports, uncategorized.
/// </summary>
public interface ISeapCategorizer
{
    Task<SeapCategorizationResult?> CategorizeAsync(
        string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken);
}

/// <summary>Lightweight category reference exposed on the SEAP tender detail page.</summary>
public sealed record SeapTenderCategoryRef(Guid Id, string Name);

/// <summary>One original file published by the contracting authority. The <see cref="DownloadUrl"/> is the
/// authenticated API route that streams the stored file (browsers cannot navigate the persisted
/// <c>local://</c>/<c>s3://</c> references directly).</summary>
public sealed record SeapTenderAttachmentRef(Guid Id, string FileName, string DownloadUrl);

/// <summary>
/// Detail projection of a SEAP tender returned by <c>GET /api/v1/seap/tenders/{id}</c>. Kept as a dedicated
/// DTO (instead of serializing the aggregate) so the page can evolve independently of persistence.
/// <c>ProcedureType</c> and <c>ProcedureState</c> are derived deterministically from the external id and the
/// deadline; the fields the current notice-search parser does not extract yet (award criterion, funding
/// source, contract duration, guarantees, qualification requirements, certificates, AI summary) are null —
/// the SPA shows elegant placeholders until full notice-detail extraction lands.
/// </summary>
public sealed record SeapTenderDetailsDto(
    Guid Id,
    string SeapExternalId,
    string Title,
    string? Description,
    IReadOnlyCollection<string> CpvCodes,
    IReadOnlyCollection<string> CpvNames,
    string? County,
    Guid? CategoryId,
    SeapTenderCategoryRef? Category,
    decimal? EstimatedValue,
    string? Currency,
    string BuyerName,
    DateTimeOffset PublicationDate,
    DateTimeOffset Deadline,
    string Status,
    string? ProposalTitle,
    string? TechnicalProposalUrl,
    string? BillOfQuantitiesUrl,
    IReadOnlyCollection<SeapTenderAttachmentRef> Attachments,
    string ProcedureType,
    string ProcedureState,
    string? AwardCriterion,
    string? FundingSource,
    string? ContractDuration,
    string? ParticipationGuarantee,
    IReadOnlyCollection<string>? QualificationRequirements,
    IReadOnlyCollection<string>? KeyPersonnel,
    IReadOnlyCollection<string>? Certificates,
    IReadOnlyCollection<string>? SummaryPoints)
{
    /// <summary>Maps the aggregate to the detail projection. The SEAP external id distinguishes the simplified
    /// procedure ("SCN" = „Procedură simplificată") from a standard contract notice ("CN" = „Licitare deschisă");
    /// the procedure state is derived from the tender deadline („În desfășurare" while accepting offers).
    /// </summary>
    public static SeapTenderDetailsDto From(SeapTender tender, DateTimeOffset now) => new(
        tender.Id,
        tender.SeapExternalId,
        tender.Title,
        tender.Description,
        tender.CpvCodes,
        tender.CpvNames,
        tender.County,
        tender.CategoryId,
        tender.Category is null ? null : new SeapTenderCategoryRef(tender.Category.Id, tender.Category.Name),
        tender.EstimatedValue,
        tender.Currency,
        tender.BuyerName,
        tender.PublicationDate,
        tender.Deadline,
        tender.Status.ToString(),
        tender.ProposalTitle,
        tender.TechnicalProposalUrl,
        tender.BillOfQuantitiesUrl,
        tender.Attachments
            .Select(attachment => new SeapTenderAttachmentRef(
                attachment.Id,
                attachment.FileName,
                $"api/v1/seap/tenders/{tender.Id}/attachments/{attachment.Id}"))
            .ToArray(),
        ProcedureType: tender.SeapExternalId.StartsWith("SCN", StringComparison.OrdinalIgnoreCase)
            ? "Procedură simplificată"
            : "Licitare deschisă",
        ProcedureState: tender.Deadline >= now ? "În desfășurare" : "Încheiată",
        AwardCriterion: null,
        FundingSource: null,
        ContractDuration: null,
        ParticipationGuarantee: null,
        QualificationRequirements: null,
        KeyPersonnel: null,
        Certificates: null,
        SummaryPoints: null);
}
