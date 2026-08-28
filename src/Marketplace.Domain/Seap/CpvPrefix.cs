using System.Text;

namespace Marketplace.Domain.Seap;

/// <summary>
/// Deterministic normalization of CPV codes/prefixes to their digit prefix used in the SEAP filter
/// (e.g. "45233000-1" or "45233000 - Lucrări de drumuri" → "45233000"; a division prefix like "45" stays
/// "45"). The database stores full CPV codes with the check-digit suffix ("45233000-1") in
/// <see cref="SeapTender.CpvCodes"/>, and matching is a prefix LIKE, so the extracted prefix is capped at
/// the 8-digit CPV code.
/// </summary>
public static class CpvPrefix
{
    /// <summary>Returns the leading digit sequence of the value (at most 8 digits), or null when the value
    /// carries no digits.</summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new StringBuilder(8);
        foreach (char character in value)
        {
            if (char.IsAsciiDigit(character))
            {
                digits.Append(character);
                if (digits.Length == 8) break;
            }
        }
        return digits.Length > 0 ? digits.ToString() : null;
    }

    /// <summary>
    /// Returns the LIKE pattern used to match tender CPV codes. CPV representative/group codes are written
    /// with trailing zeros (e.g. "45210000" for the 4521 buildings class, "45233000" for the 4523 roads
    /// class, "15000000" for the 15 food division), so the pattern widens to the meaningful level instead of
    /// demanding an exact 8-digit prefix that granular tender codes (45214200-2, 45215120-4, 45233100-2)
    /// never share: the 4-digit CPV class for any code (2 digits only when the value is a whole division —
    /// six+ trailing zeros, e.g. 15000000 → 15). A short prefix like "45" stays "45%". Returns "%" for
    /// invalid values.
    /// </summary>
    public static string ToMatchPattern(string? value)
    {
        string? prefix = TryNormalize(value);
        if (prefix is null) return "%";
        int length = prefix.Length;
        int trailingZeros = 0;
        while (trailingZeros < length && prefix[length - 1 - trailingZeros] == '0') trailingZeros++;
        int width = trailingZeros >= 6 ? 2 : Math.Min(4, length);
        return prefix[..width] + "%";
    }
}
