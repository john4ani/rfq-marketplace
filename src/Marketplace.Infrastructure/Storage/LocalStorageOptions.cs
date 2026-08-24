namespace Marketplace.Infrastructure.Storage;

/// <summary>
/// Local-first storage configuration (section <c>Storage</c>). <see cref="UseLocalStorage"/> swaps
/// object storage to the local file system (development) instead of Cloudflare R2 / AWS S3 (prod).
/// </summary>
public sealed class LocalStorageOptions
{
    public const string SectionName = "Storage";

    public bool UseLocalStorage { get; init; }

    /// <summary>Root directory for locally stored files (relative to the API working directory).</summary>
    public string LocalPath { get; init; } = "App_Data/Storage";
}
