namespace Marketplace.Domain.SupplierDiscovery;

/// <summary>
/// Determines whether two externally sourced company names describe the same commercial entity.
/// This is deliberately conservative: legal suffixes and descriptive trailing terms may differ,
/// but a source with conflicting known geography must not be merged by the caller.
/// </summary>
public static class CompanyEvidenceIdentity
{
    public static bool AreSame(string left, string right)
    {
        string[] leftTokens = MeaningfulTokens(left);
        string[] rightTokens = MeaningfulTokens(right);
        if (leftTokens.Length == 0 || rightTokens.Length == 0) return false;

        return leftTokens.SequenceEqual(rightTokens)
            || leftTokens.All(token => rightTokens.Contains(token, StringComparer.Ordinal))
            || rightTokens.All(token => leftTokens.Contains(token, StringComparer.Ordinal));
    }

    public static bool IsCompatibleEvidence(
        string leftName, string? leftLocality, string? leftCounty,
        string rightName, string? rightLocality, string? rightCounty) =>
        AreSame(leftName, rightName)
        && SameOrMissing(leftLocality, rightLocality)
        && SameOrMissing(leftCounty, rightCounty);

    private static string[] MeaningfulTokens(string value) => value.Trim().ToUpperInvariant()
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(token => token is not "SRL" and not "SA" and not "SCA" and not "SNC" && token.Length >= 4)
        .ToArray();

    private static bool SameOrMissing(string? left, string? right) =>
        string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)
        || string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
