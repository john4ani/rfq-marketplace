namespace Marketplace.Domain.Seap;

/// <summary>
/// One versioned, deterministic preparation of a single original published document (Stage 1.5). The versioned
/// identity is (AttachmentId, SourceContentSha256, ExtractionVersion): the same file bytes prepared with the
/// same extractor version are prepared exactly once (cached); a content change or an extractor version bump
/// produces a NEW preparation row. Deliberately separate from <see cref="SeapTenderAttachment"/> and from any
/// <see cref="SeapIngestionRun"/> — text is never stored on those. Pages and sections are persisted in their
/// own tables (<see cref="SeapPreparedDocumentPage"/>, <see cref="SeapPreparedDocumentSection"/>).
/// </summary>
public sealed class SeapDocumentPreparation
{
    private SeapDocumentPreparation() { }

    public Guid Id { get; private set; }
    public Guid TenderId { get; private set; }
    public Guid AttachmentId { get; private set; }
    /// <summary>SHA-256 (hex, lowercase) of the exact published bytes that were prepared.</summary>
    public string SourceContentSha256 { get; private set; } = string.Empty;
    /// <summary>Extractor version string (e.g. „datasheet-pdfpig-v1"). Part of the cache key.</summary>
    public string ExtractionVersion { get; private set; } = string.Empty;
    public SeapDocumentPreparationStatus Status { get; private set; } = SeapDocumentPreparationStatus.Pending;
    public bool HasTextLayer { get; private set; }
    public bool NeedsOcr { get; private set; }
    public int PageCount { get; private set; }
    /// <summary>Percentage of the document letters captured inside delimited sections (0-100), null when the
    /// preparation failed before any measurement.</summary>
    public int? CoveragePercent { get; private set; }
    public int WarningCount { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SeapDocumentPreparation Create(
        Guid tenderId, Guid attachmentId, string sourceContentSha256, string extractionVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sourceContentSha256))
            throw new ArgumentException("Hash-ul de conținut este obligatoriu.", nameof(sourceContentSha256));
        if (string.IsNullOrWhiteSpace(extractionVersion))
            throw new ArgumentException("Versiunea extractorului este obligatorie.", nameof(extractionVersion));
        return new SeapDocumentPreparation
        {
            Id = Guid.NewGuid(),
            TenderId = tenderId,
            AttachmentId = attachmentId,
            SourceContentSha256 = sourceContentSha256.Trim().ToLowerInvariant(),
            ExtractionVersion = extractionVersion.Trim(),
            Status = SeapDocumentPreparationStatus.Pending,
            CreatedAt = now
        };
    }

    /// <summary>Marks the attempt in flight (a lost lease can never complete it — the caller re-reads the row
    /// under its own claim before persisting the outcome). AttemptCount counts processing attempts.</summary>
    public void BeginAttempt(DateTimeOffset now)
    {
        Status = SeapDocumentPreparationStatus.Preparing;
        StartedAt ??= now;
        AttemptCount += 1;
    }

    /// <summary>Terminal outcome: stores the extraction facts and releases the preparation for display.</summary>
    public void Complete(
        SeapDocumentPreparationStatus status,
        int pageCount,
        bool hasTextLayer,
        bool needsOcr,
        int? coveragePercent,
        int warningCount,
        DateTimeOffset now,
        string? errorCode = null,
        string? errorMessage = null)
    {
        Status = status;
        PageCount = pageCount;
        HasTextLayer = hasTextLayer;
        NeedsOcr = needsOcr;
        CoveragePercent = coveragePercent;
        WarningCount = warningCount;
        CompletedAt = now;
        if (errorCode is not null)
        {
            LastErrorCode = errorCode;
            LastErrorMessage = errorMessage;
        }
    }
}

/// <summary>One extracted page of a prepared document (ordered text per page, with the page-level noise flag).</summary>
public sealed class SeapPreparedDocumentPage
{
    private SeapPreparedDocumentPage() { }

    public Guid Id { get; private set; }
    public Guid PreparationId { get; private set; }
    public int PageNumber { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int LetterCount { get; private set; }
    public string? Warning { get; private set; }

    public static SeapPreparedDocumentPage Create(Guid preparationId, int pageNumber, string text, string? warning = null)
    {
        string normalized = text?.Trim() ?? string.Empty;
        return new SeapPreparedDocumentPage
        {
            Id = Guid.NewGuid(),
            PreparationId = preparationId,
            PageNumber = pageNumber,
            Text = normalized,
            LetterCount = normalized.Count(char.IsLetter),
            Warning = string.IsNullOrWhiteSpace(warning) ? null : warning.Trim()
        };
    }
}

/// <summary>One delimited section of a prepared document. <see cref="RawCode"/> is the printed SEAP code kept as
/// the faithful citation (I.5 vs I.6 stay distinct); <see cref="NormalizedRole"/> is the stable logical key
/// (deliberately a versioned string, not a rigid enum — the deterministic extractor may discover new roles).</summary>
public sealed class SeapPreparedDocumentSection
{
    private SeapPreparedDocumentSection() { }

    public Guid Id { get; private set; }
    public Guid PreparationId { get; private set; }
    public int Ordinal { get; private set; }
    public string RawCode { get; private set; } = string.Empty;
    public string NormalizedRole { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public int StartPage { get; private set; }
    public int EndPage { get; private set; }
    public int? LotIndex { get; private set; }
    public string Text { get; private set; } = string.Empty;

    public static SeapPreparedDocumentSection Create(
        Guid preparationId,
        int ordinal,
        string rawCode,
        string normalizedRole,
        string title,
        int startPage,
        int endPage,
        int? lotIndex,
        string text)
    {
        return new SeapPreparedDocumentSection
        {
            Id = Guid.NewGuid(),
            PreparationId = preparationId,
            Ordinal = ordinal,
            RawCode = string.IsNullOrWhiteSpace(rawCode) ? "RAW" : rawCode.Trim(),
            NormalizedRole = string.IsNullOrWhiteSpace(normalizedRole) ? "Unknown" : normalizedRole.Trim(),
            Title = title?.Trim() ?? string.Empty,
            StartPage = Math.Max(1, startPage),
            EndPage = Math.Max(1, endPage),
            LotIndex = lotIndex,
            Text = text?.Trim() ?? string.Empty
        };
    }
}

