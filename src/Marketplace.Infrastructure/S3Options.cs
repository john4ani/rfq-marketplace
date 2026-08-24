namespace Marketplace.Infrastructure;

/// <summary>
/// Shared S3-compatible object-storage settings used by every V3 storage service (SEAP attachments,
/// auto-bid documents, Company Vault uploads).
/// By default the client uses the AWS IAM credential chain against the configured <see cref="Region"/>.
/// When <see cref="ServiceUrl"/> is set, static credentials and path-style addressing are used instead —
/// this is how the same code targets Cloudflare R2 (S3-compatible) without changing application logic.
/// <see cref="RootPrefix"/> isolates environments inside one bucket (e.g. "prod/" vs "staging/").
/// </summary>
public sealed class S3Options
{
    public const string SectionName = "S3";

    /// <summary>R2-compatible endpoint (e.g. "https://&lt;account-id&gt;.r2.cloudflarestorage.com"). Empty = AWS S3.</summary>
    public string ServiceUrl { get; init; } = string.Empty;

    /// <summary>Static access key used with <see cref="ServiceUrl"/> (R2 Access Key ID).</summary>
    public string AccessKeyId { get; init; } = string.Empty;

    /// <summary>Static secret key used with <see cref="ServiceUrl"/> (R2 Secret Access Key).</summary>
    public string SecretAccessKey { get; init; } = string.Empty;

    /// <summary>AWS region (defaults to eu-central-1). Used for AWS S3 and as the R2 authentication region.</summary>
    public string Region { get; init; } = "eu-central-1";

    /// <summary>
    /// Root object-key prefix separating environments, e.g. "prod" or "staging". Empty disables the prefix.
    /// Normalized by <see cref="NormalizedRootPrefix"/> (leading/trailing slashes trimmed, trailing slash added).
    /// </summary>
    public string RootPrefix { get; init; } = string.Empty;

    public string NormalizedRootPrefix =>
        string.IsNullOrWhiteSpace(RootPrefix) ? string.Empty : RootPrefix.Trim().Trim('/') + "/";
}
