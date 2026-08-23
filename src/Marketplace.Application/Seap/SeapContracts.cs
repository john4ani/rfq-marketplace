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
    IReadOnlyCollection<SeapAttachmentDto> Attachments);

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
