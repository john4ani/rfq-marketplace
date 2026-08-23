using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Pure mapping from the SEAP notice shape to the SeapTender aggregate. No I/O here, so it is
/// directly unit-testable with sample JSON payloads.
/// </summary>
public static class SeapTenderMapper
{
    public static SeapTender MapNotice(SeapTenderNoticeDto notice, DateTimeOffset now) =>
        SeapTender.Import(
            notice.Id,
            notice.Title,
            notice.Description,
            notice.CpvCodes,
            notice.EstimatedValue,
            notice.Currency,
            notice.BuyerName,
            notice.PublicationDate,
            notice.Deadline,
            now);
}
