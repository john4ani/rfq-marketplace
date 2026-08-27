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
            notice.CpvNames ?? [],
            notice.EstimatedValue,
            notice.Currency,
            notice.BuyerName,
            notice.County,
            // The live e-licitatie.ro payload carries Romania's local offset (+03:00 / +02:00).
            // Npgsql only writes offset 0 (UTC) into `timestamp with time zone`, so normalize at the
            // wire boundary before persistence.
            notice.PublicationDate.ToUniversalTime(),
            notice.Deadline.ToUniversalTime(),
            now);
}
