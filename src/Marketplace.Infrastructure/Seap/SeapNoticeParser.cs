using System.Text.Json;
using Marketplace.Application.Seap;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Pure parser for the real public e-licitatie.ro notice-search payload. Maps the real envelope
/// (<c>{ total, items, searchTooLong }</c>) to the importer DTO and normalizes every optional wire field
/// (<c>cpvCodeAndName</c>, <c>estimatedValueRon</c>, tender deadlines, …) so downstream mapping never
/// sees null where an empty collection is expected. Empty or non-JSON payloads (an HTML error page, an
/// error array, …) degrade to an empty result instead of throwing NullReferenceException.
/// </summary>
public static class SeapNoticeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyCollection<SeapTenderNoticeDto> ParseNotices(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        SeapNoticeSearchResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<SeapNoticeSearchResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return [];
        }

        return response?.Items?.Select(MapToDto).ToArray() ?? [];
    }

    private static SeapTenderNoticeDto MapToDto(SeapNoticeSearchItem item) => new(
        item.NoticeNo ?? string.Empty,
        item.ContractTitle ?? string.Empty,
        null,
        ParseCpvCodes(item.CpvCodeAndName),
        item.EstimatedValueRon,
        "RON",
        item.ContractingAuthorityNameAndFN ?? string.Empty,
        item.NoticeStateDate,
        item.MaxTenderReceiptDeadline ?? item.MinTenderReceiptDeadline ?? DateTimeOffset.MinValue,
        []);

    // Real SEAP CPV shape: "71319000-7 - Servicii de expertiza (Rev.2)" — keep the code, drop the label.
    private static IReadOnlyCollection<string> ParseCpvCodes(string? cpvCodeAndName) =>
        string.IsNullOrWhiteSpace(cpvCodeAndName) ? [] : [cpvCodeAndName.Split(" - ", 2)[0].Trim()];
}
