using System.Text.RegularExpressions;

namespace Marketplace.Domain.Seap;

/// <summary>Parses/validates the SEAP external notice numbers (e.g. <c>SCN1179200</c>, <c>CN1010518</c>).
/// The direct-hit resolve endpoint accepts any case and surrounding whitespace, then stores the normalized
/// form on <see cref="SeapTender.NormalizedExternalId"/> (unique index → idempotent upsert).</summary>
public static partial class SeapNoticeNumber
{
    [GeneratedRegex("^(SCN|CN)\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoticeNumberPattern();

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value?.Trim() ?? string.Empty).ToUpperInvariant();
        return normalized.Length is > 0 and <= 32 && NoticeNumberPattern().IsMatch(normalized);
    }
}
