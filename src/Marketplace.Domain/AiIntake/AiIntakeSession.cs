namespace Marketplace.Domain.AiIntake;

public enum AiIntakeSessionStatus { InProgress, ReadyForApproval, Approved }

public sealed class AiIntakeSession
{
    private AiIntakeSession() { }

    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public string InitialInput { get; private set; } = string.Empty;
    public string TranscriptJson { get; private set; } = "[]";
    public string ExtractionJson { get; private set; } = "{}";
    public AiIntakeSessionStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public Guid? ApprovedRfqId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public static AiIntakeSession Create(Guid id, Guid buyerId, string initialInput, DateTimeOffset now)
    {
        string cleanInput = initialInput?.Trim() ?? string.Empty;
        if (id == Guid.Empty || buyerId == Guid.Empty)
            throw new ArgumentException("Session and Buyer identifiers are required.");
        if (cleanInput.Length is < 10 or > 4000)
            throw new AiIntakeRuleException("Descrierea nevoii trebuie să aibă între 10 și 4.000 de caractere.");

        return new AiIntakeSession
        {
            Id = id,
            BuyerId = buyerId,
            InitialInput = cleanInput,
            Status = AiIntakeSessionStatus.InProgress,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ApplyExtraction(
        string transcriptJson,
        string extractionJson,
        string provider,
        string model,
        string promptVersion,
        int inputTokens,
        int outputTokens,
        bool requiresClarification,
        DateTimeOffset now)
    {
        if (Status == AiIntakeSessionStatus.Approved)
            throw new AiIntakeRuleException("Draftul asistat a fost deja aprobat.");
        if (string.IsNullOrWhiteSpace(extractionJson))
            throw new ArgumentException("A structured extraction is required.", nameof(extractionJson));
        if (inputTokens < 0 || outputTokens < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Token usage cannot be negative.");

        TranscriptJson = string.IsNullOrWhiteSpace(transcriptJson) ? "[]" : transcriptJson;
        ExtractionJson = extractionJson;
        Provider = Clean(provider, 80, nameof(provider));
        Model = Clean(model, 120, nameof(model));
        PromptVersion = Clean(promptVersion, 40, nameof(promptVersion));
        InputTokens = checked(InputTokens + inputTokens);
        OutputTokens = checked(OutputTokens + outputTokens);
        Status = requiresClarification ? AiIntakeSessionStatus.InProgress : AiIntakeSessionStatus.ReadyForApproval;
        UpdatedAt = now;
    }

    public void Approve(Guid rfqId, DateTimeOffset now)
    {
        if (rfqId == Guid.Empty) throw new ArgumentException("The approved request identifier is required.", nameof(rfqId));
        if (Status == AiIntakeSessionStatus.Approved)
        {
            if (ApprovedRfqId == rfqId) return;
            throw new AiIntakeRuleException("Draftul asistat a fost deja aprobat pentru o altă cerere.");
        }
        if (Status != AiIntakeSessionStatus.ReadyForApproval)
            throw new AiIntakeRuleException("Răspunde la clarificarea curentă înainte de a aproba draftul.");

        ApprovedRfqId = rfqId;
        Status = AiIntakeSessionStatus.Approved;
        UpdatedAt = now;
    }

    public void CompleteInAdvancedForm(DateTimeOffset now)
    {
        if (Status == AiIntakeSessionStatus.Approved) return;
        Status = AiIntakeSessionStatus.ReadyForApproval;
        UpdatedAt = now;
    }

    private static string Clean(string value, int maxLength, string parameterName)
    {
        string clean = value?.Trim() ?? string.Empty;
        if (clean.Length is 0 || clean.Length > maxLength)
            throw new ArgumentException($"{parameterName} must contain between 1 and {maxLength} characters.", parameterName);
        return clean;
    }
}

public sealed class AiIntakeRuleException(string message) : InvalidOperationException(message);
