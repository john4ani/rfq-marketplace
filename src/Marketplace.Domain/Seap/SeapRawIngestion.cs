namespace Marketplace.Domain.Seap;

public enum SeapRawIngestionStatus { Pending, Parsed, Failed }

/// <summary>
/// Staging record for raw SEAP notices. Nothing reaches the public <see cref="SeapTender"/> table until a
/// notice parses and maps successfully (Status = <see cref="SeapRawIngestionStatus.Parsed"/>); Failed rows
/// keep the payload and error message for diagnostics. Live users never see half-processed announcements.
/// </summary>
public sealed class SeapRawIngestion
{
    private SeapRawIngestion() { }

    public Guid Id { get; private set; }
    public string NumarAnunt { get; private set; } = string.Empty;
    public string JsonPayload { get; private set; } = string.Empty;
    public SeapRawIngestionStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SeapRawIngestion Pending(string numarAnunt, string jsonPayload, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        NumarAnunt = numarAnunt,
        JsonPayload = jsonPayload,
        Status = SeapRawIngestionStatus.Pending,
        CreatedAt = now,
        UpdatedAt = now
    };

    public void MarkParsed(DateTimeOffset now)
    {
        Status = SeapRawIngestionStatus.Parsed;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, DateTimeOffset now)
    {
        Status = SeapRawIngestionStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAt = now;
    }
}
