using System.Text.Json;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;

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
        [],
        CpvNames: ParseCpvNames(item.CpvCodeAndName),
        County: ParseCounty(item.ContractingAuthorityNameAndFN, item.ContractTitle),
        NoticeId: item.NoticeId,
        SysNoticeTypeId: item.SysNoticeTypeId,
        ProcedureStateText: item.SysProcedureState?.Text);

    /// <summary>
    /// Parses the public notice-detail API response (<c>GetDfNoticeSectionFiles</c>): the envelope
    /// <c>{ isPubOrSu, dfNoticeDocs: [{ noticeDocumentCode, noticeDocumentName, noticeDocumentUrl }] }</c>.
    /// Old notices (whose files were purged) and non-JSON/error payloads (e.g. the real <c>["Eroare de
    /// sistem …"]</c> array) degrade to an empty result — the document list is enrichment, never a gate.
    /// </summary>
    public static IReadOnlyCollection<SeapNoticeDocumentDto> ParseNoticeDocuments(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        JsonDocument? document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("dfNoticeDocs", out JsonElement docs)
                || docs.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<SeapNoticeDocumentDto>();
            foreach (JsonElement doc in docs.EnumerateArray())
            {
                if (doc.ValueKind != JsonValueKind.Object) continue;
                string? code = ReadString(doc, "noticeDocumentCode");
                string? name = ReadString(doc, "noticeDocumentName");
                string? url = ReadString(doc, "noticeDocumentUrl");
                // The platform's section/type label (e.g. „Documente DUAE") is not part of the observed
                // dfNoticeDocs shape, but it is read when present so the dossier classification can prefer it.
                string? section = ReadString(doc, "noticeDocumentSection");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                result.Add(new SeapNoticeDocumentDto(code ?? string.Empty, name, url, Section: section));
            }
            return result;
        }
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // Real SEAP CPV shape: "71319000-7 - Servicii de expertiza (Rev.2)" — keep the code, drop the label.
    private static IReadOnlyCollection<string> ParseCpvCodes(string? cpvCodeAndName) =>
        string.IsNullOrWhiteSpace(cpvCodeAndName) ? [] : [cpvCodeAndName.Split(" - ", 2)[0].Trim()];

    // The human-readable CPV label ("Servicii de expertiza") is the input to the AI categorizer, so it is
    // kept as well; the "(Rev.X)" suffix is dropped.
    /// <summary>Deterministic county of a search notice. The platform's search payload carries no structured
    /// county/UAT field, so the county is derived from the contracting-authority name — with the contract
    /// title as a fallback for authorities that only spell the county in the notice title (e.g. „comuna X,
    /// județul Suceava"). The authority name wins to avoid attributing a cross-county worksite mentioned in
    /// the title to the wrong home county.</summary>
    private static string? ParseCounty(string? authorityName, string? contractTitle)
    {
        string? county = CountyParser.Parse(authorityName);
        if (county is not null) return county;
        if (string.IsNullOrWhiteSpace(contractTitle)) return null;
        return CountyParser.Parse($"{authorityName} {contractTitle}");
    }

    private static IReadOnlyCollection<string> ParseCpvNames(string? cpvCodeAndName)
    {
        if (string.IsNullOrWhiteSpace(cpvCodeAndName)) return [];
        string[] parts = cpvCodeAndName.Split(" - ", 2);
        if (parts.Length < 2) return [];
        string name = parts[1];
        int revisionIndex = name.IndexOf(" (Rev.", StringComparison.Ordinal);
        if (revisionIndex > 0) name = name[..revisionIndex];
        return [name.Trim()];
    }
}
