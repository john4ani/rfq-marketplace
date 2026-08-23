namespace Marketplace.Domain.Seap;

/// <summary>One downloaded attachment (ZIP/PDF) of a SEAP tender, stored in S3.</summary>
public sealed class SeapTenderAttachment
{
    private SeapTenderAttachment() { }

    public Guid Id { get; private set; }
    public Guid TenderId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string S3Url { get; private set; } = string.Empty;
    public DateTimeOffset DownloadedAt { get; private set; }

    public static SeapTenderAttachment Create(Guid tenderId, string fileName, string s3Url, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        TenderId = tenderId,
        FileName = Required(fileName, "Numele fișierului este obligatoriu."),
        S3Url = Required(s3Url, "Adresa fișierului este obligatorie."),
        DownloadedAt = now
    };

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
