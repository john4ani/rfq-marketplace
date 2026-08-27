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
