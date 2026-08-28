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
}
