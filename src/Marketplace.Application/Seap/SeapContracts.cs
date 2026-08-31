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
    string? County = null,
    /// <summary>Numeric id of the notice in SEAP; used to fetch the published document list.</summary>
    int? NoticeId = null,
    /// <summary>Notice type (2 = CN, 17 = SCN); required alongside <see cref="NoticeId"/> for the documents.</summary>
    int? SysNoticeTypeId = null);

/// <summary>One document published by the contracting authority for a notice (Fișa de date, Caiet de
/// sarcini, formulare, model contract). Resolved via the public notice-detail API
/// (<c>GetDfNoticeSectionFiles</c>), which the notice-search payload does not carry.</summary>
public sealed record SeapNoticeDocumentDto(
    string Code,
    string FileName,
    /// <summary>Absolute e-licitatie.ro URL that streams the file (requires a same-origin Referer).</summary>
    string DownloadUrl);

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
    DateTimeOffset? MaxTenderReceiptDeadline,
    /// <summary>Numeric id of the notice in SEAP (the search payload carries it); used to fetch the
    /// published document list (Fișa de date, Caiet de sarcini, formulare) via the public notice-detail API.</summary>
    int? NoticeId = null,
    /// <summary>Notice type (2 = contract notice CN, 17 = simplified procedure SCN); required alongside
    /// <see cref="NoticeId"/> to resolve the published documents.</summary>
    int? SysNoticeTypeId = null);

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

    /// <summary>
    /// Fetches the published documents of a notice (Fișa de date, Caiet de sarcini, formulare, model
    /// contract) from the public e-licitatie.ro notice-detail API (<c>GetDfNoticeSectionFiles</c>). The
    /// notice-search payload does not carry them. Returns an empty collection on any failure (the import
    /// must never gate on the document list).
    /// </summary>
    Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken);
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

/// <summary>
/// AI summarizer for a SEAP tender: sends the available notice data (title, CPV, estimated value, buyer,
/// county, deadlines, description when present) — plus the text of already-processed attachments (Fișa de
/// Date / Caiet de Sarcini) when the ingestion pipeline has extracted it — to the shared <c>ILlmProvider</c>
/// and returns structured, decision-critical values („Sinteză AI" bullets, award criterion, participation
/// guarantee, funding source, duration, qualification requirements, key personnel). Enrichment is never a
/// gate: every failure degrades to null and the tender still imports without AI details.
/// </summary>
public interface ISeapSummaryGenerator
{
    Task<SeapAiExtraction?> GenerateAsync(
        SeapTender tender, string? attachmentText, CancellationToken cancellationToken);
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
/// deadline; the AI-extracted fields (award criterion, funding source, contract duration, guarantees,
/// qualification requirements, key personnel, summary bullets) carry real values once the AI summarizer ran
/// at import and are null before that — the SPA shows a skeleton/placeholders until then. <c>Certificates</c>
/// stays null (not part of the extraction contract yet).
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
        AwardCriterion: tender.AwardCriterion,
        FundingSource: tender.FundingSource,
        ContractDuration: tender.ContractDuration,
        ParticipationGuarantee: tender.ParticipationGuarantee,
        QualificationRequirements: tender.QualificationRequirements is null ? null : [tender.QualificationRequirements],
        KeyPersonnel: tender.KeyPersonnel is null ? null : [tender.KeyPersonnel],
        Certificates: null,
        SummaryPoints: tender.SummaryPoints.Count > 0 ? tender.SummaryPoints : null);
}
