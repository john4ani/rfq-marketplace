using Marketplace.Domain.Seap;

namespace Marketplace.Application.Seap;

/// <summary>Raw tender notice shape returned by the public e-licitatie.ro JSON API.</summary>
public sealed record SeapTenderNoticeDto(
    string Id,
    string Title,
    string? Description,
    IReadOnlyCollection<string> CpvCodes,
    decimal? EstimatedValue,
    string? Currency,
    string BuyerName,
    DateTimeOffset PublicationDate,
    DateTimeOffset Deadline,
    IReadOnlyCollection<SeapAttachmentDto> Attachments,
    /// <summary>Human-readable CPV labels (e.g. "Montare de garduri"), aligned with <see cref="CpvCodes"/>.</summary>
    IReadOnlyCollection<string>? CpvNames = null,
    /// <summary>County derived from <see cref="BuyerName"/> (e.g. "Vaslui"); null when unrecognizable.</summary>
    string? County = null,
    /// <summary>Numeric id of the notice in SEAP; used to fetch the published document list.</summary>
    int? NoticeId = null,
    /// <summary>Notice type (2 = CN, 17 = SCN); required alongside <see cref="NoticeId"/> for the documents.</summary>
    int? SysNoticeTypeId = null,
    /// <summary>Raw procedure-state text reported by SEAP for this notice (e.g. „Anulata", „Atribuita",
    /// „In derulare"). Null when the search payload carried no state. Displayed/derived on the tender detail
    /// instead of guessing from the deadline alone (an annulled procedure must never look „În desfășurare").</summary>
    string? ProcedureStateText = null,
    /// <summary>Numeric „cNoticeId" of the notice in SEAP (the search payload carries BOTH cNoticeId and
    /// noticeId). The published-document manifest endpoint (<c>GetDfNoticeSectionFiles</c>) resolves SCN
    /// notices by cNoticeId — persisted separately from <see cref="NoticeId"/> so the manifest can always be
    /// fetched for simplified procedures.</summary>
    int? CNoticeId = null);

/// <summary>One document published by the contracting authority for a notice (Fișa de date, Caiet de
/// sarcini, formulare, model contract). Resolved via the public notice-detail API
/// (<c>GetDfNoticeSectionFiles</c>), which the notice-search payload does not carry.</summary>
public sealed record SeapNoticeDocumentDto(
    string Code,
    string FileName,
    /// <summary>Absolute e-licitatie.ro URL that streams the file (requires a same-origin Referer).</summary>
    string DownloadUrl,
    /// <summary>Machine-readable section/type label the platform groups the file under (e.g. „Documente DUAE"),
    /// when the notice-detail payload exposes one. Drives the dossier classification before the file-name
    /// fallback; null when the platform gives no section (the observed <c>GetDfNoticeSectionFiles</c> shape
    /// today carries only code + name + url).</summary>
    string? Section = null);

public sealed record SeapAttachmentDto(string FileName, string DownloadUrl);

/// <summary>State reference carried by the SEAP search payload for a notice/its procedure
/// (e.g. <c>{ id: 3, text: "Anulata" }</c>). Kept as an id+text pair so the original value is preserved.</summary>
public sealed record SeapStateRef(int? Id, string? Text);

/// <summary>
/// Thrown when the live e-licitatie.ro platform cannot answer a direct notice-number lookup at all —
/// transport failure/timeout, an HTTP 4xx/5xx answer (403 bot-block, 500) or a non-JSON payload. The API
/// maps it to a 502 Bad Gateway with a friendly detail (never a misleading 404). It is NOT thrown when the
/// platform answered normally and simply does not know the number — that case is a plain null result.
/// </summary>
public sealed class SeapSourceUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Thrown when the published-document manifest endpoint (<c>GetDfNoticeSectionFiles</c>) rejects the
/// request itself (HTTP 400 — a contract/identifier problem, e.g. an SCN queried by its noticeId instead of
/// its cNoticeId). A rejection is permanent for that request: the run must FAIL immediately with
/// <c>SeapDocumentManifestRejected</c> — never Partial (an empty dossier cannot be told from „no documents"
/// when the manifest itself was never obtained) and never an unbounded automatic retry. 403/429/5xx and
/// transport errors keep using the normal transient-retry policy (<see cref="SeapSourceUnavailableException"/>).
/// </summary>
public sealed class SeapDocumentManifestRejectedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>Thrown when the global cap of simultaneously active resolve runs is reached (cost guardrail).
/// Mapped by the API to HTTP 429 so the client can retry later instead of queueing unbounded LLM work.</summary>
public sealed class SeapResolveLimitReachedException(string message) : Exception(message);

public sealed record SeapTenderImportResult(SeapTender? Tender, int DownloadedAttachmentCount, bool Duplicate);

/// <summary>Outcome of <c>POST /api/v1/seap/tenders/resolve</c>. <c>Status</c> is <see cref="SeapIngestionStatus.Ready"/>
/// when the local tender already has a document-grounded analysis (HTTP 200 returns <see cref="ReadyTender"/>, the
/// full detail DTO); otherwise the status is the persisted run status (<c>Pending</c> until the ingestion worker
/// runs — HTTP 202 returns only <see cref="TenderId"/>/<see cref="RunId"/>/<see cref="Status"/>, never a big DTO).</summary>
public sealed record SeapResolveResult(
    Guid TenderId,
    string NoticeNo,
    string Status,
    Guid? RunId,
    SeapTenderDetailsDto? ReadyTender,
    string? Purpose = null);

/// <summary>Status payload of <c>GET /api/v1/seap/tenders/{id}/ingestion-status</c>. Status is derived from the
/// actual tender state: a document-grounded analysis wins (<see cref="SeapIngestionStatus.Ready"/>); otherwise the
/// latest run status is reported. The remaining fields are the tender-level markers that let the future worker
/// skip unchanged document sets (<c>DocumentSetHash</c>) and re-run only when the analysis schema changed
/// (<c>AnalysisVersion</c>).</summary>
public sealed record SeapIngestionStatusDto(
    Guid TenderId,
    string NoticeNo,
    string Status,
    string ImportMode,
    Guid? RunId,
    int AttemptCount,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? LastErrorCode,
    string? LastErrorMessage,
    string? DocumentSetHash,
    int? AnalysisVersion,
    string? DigestStatus = null,
    DateTimeOffset? DocumentsFetchedAt = null,
    DateTimeOffset? TextExtractedAt = null,
    string? Purpose = null,
    int DownloadedDocumentCount = 0,
    int FailedDocumentCount = 0);

/// <summary>Envelope returned by the real public e-licitatie.ro notice search (GetCNoticeList).</summary>
public sealed record SeapNoticeSearchResponse(
    int Total,
    IReadOnlyCollection<SeapNoticeSearchItem> Items,
    bool SearchTooLong);

/// <summary>Projection of a real e-licitatie.ro search item; only the fields the importer needs. All
/// optional fields may be missing or null in real payloads — the parser normalizes them to empty/null
/// instead of throwing NullReferenceException.</summary>
public sealed record SeapNoticeSearchItem(
    string? NoticeNo,
    string? ContractTitle,
    string? ContractingAuthorityNameAndFN,
    string? CpvCodeAndName,
    decimal? EstimatedValueRon,
    DateTimeOffset NoticeStateDate,
    DateTimeOffset? MinTenderReceiptDeadline,
    DateTimeOffset? MaxTenderReceiptDeadline,
    /// <summary>Numeric id of the notice in SEAP (the search payload carries it); used to fetch the
    /// published document list (Fișa de date, Caiet de sarcini, formulare) via the public notice-detail API.</summary>
    int? NoticeId = null,
    /// <summary>Notice type (2 = contract notice CN, 17 = simplified procedure SCN); required alongside
    /// <see cref="NoticeId"/> to resolve the published documents.</summary>
    int? SysNoticeTypeId = null,
    /// <summary>Procedure lifecycle state reported by SEAP (e.g. „Anulata", „Atribuita", „In derulare").
    /// The deadline alone cannot distinguish an annulled/awarded procedure from one still accepting offers.</summary>
    SeapStateRef? SysProcedureState = null,
    /// <summary>Numeric „cNoticeId" of the notice (the search payload carries BOTH cNoticeId and noticeId).
    /// The published-document manifest endpoint resolves simplified procedures (SCN) by cNoticeId.</summary>
    int? CNoticeId = null,
    /// <summary>Numeric procedure id reported by SEAP; kept for diagnostics and future notice-detail calls.</summary>
    int? ProcedureId = null);

public interface ISeapApiClient
{
    Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken);

    /// <summary>Fetches one page of notices from the live public SEAP search, optionally restricted to a
    /// publication-date window. Live-only: the on-demand „Caută în SEAP" search never falls back to the
    /// local golden payloads. Returns an empty collection on any failure.</summary>
    Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
        int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Direct notice-number lookup („direct hit", e.g. <c>SCN1179200</c> / <c>CN1010518</c>): asks the live
    /// public SEAP search for the exact notice and returns it with the numeric <c>NoticeId</c>/
    /// <c>SysNoticeTypeId</c> populated (the search payload carries them), so the published documents can be
    /// resolved afterwards. Best-effort by design: the anonymous search is first probed with a notice-number
    /// filter key (<c>sysNoticeNo</c> — not documented on the public API); when the platform ignores it, the
    /// client falls back to a bounded scan of the most recent window. Returns null when SEAP answered
    /// normally and does not know the number; throws <see cref="SeapSourceUnavailableException"/> when the
    /// platform is unreachable, answers 4xx/5xx or returns a non-JSON payload.
    /// </summary>
    Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the published documents of a notice (Fișa de date, Caiet de sarcini, formulare, model
    /// contract) from the public e-licitatie.ro notice-detail API (<c>GetDfNoticeSectionFiles</c>). The
    /// notice-search payload does not carry them. Returns an empty collection on any failure (the import
    /// must never gate on the document list).
    /// </summary>
    Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
        int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken);
}

public interface ISeapAttachmentStorage
{
    Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken);
}

/// <summary>Outcome of durably persisting one original SEAP published document into the Direct Hit dossier store.</summary>
public sealed record StoredSeapOriginalDocument(string Reference, string ContentSha256, long SizeBytes);

/// <summary>A durably stored original opened for reading (the caller owns the stream). Content type comes from
/// the stored object; the caller serves the original file name from the attachment row.</summary>
public sealed record SeapOriginalDocumentContent(Stream Content, string ContentType);

/// <summary>
/// Thrown by <see cref="ISeapOriginalDocumentStore.StoreOriginalAsync"/> when a single published document
/// exceeds the configured per-file dossier limit (<c>Seap:MaxOriginalDocumentBytes</c>). The check runs both
/// against a known declared length (Content-Length / stream length) before anything is uploaded AND again while
/// streaming, so a chunked/unknown-length download can never slip past the guard; any partially written object
/// is removed before the exception surfaces.
/// </summary>
public sealed class SeapOriginalDocumentTooLargeException(long actualBytes, long maxBytes)
    : Exception($"Fișierul ({actualBytes} bytes) depășește limita permisă pentru un document original ({maxBytes} bytes).");

/// <summary>
/// Stage 1 (DocumentImport) dossier storage for the ORIGINAL published documents of a Direct Hit tender —
/// deliberately separate from <see cref="ISeapAttachmentStorage"/> (which covers on-demand browsing copies).
/// Originals are persisted durably under their own logically separate storage namespace, never mixed with
/// user uploads or browsing copies, so Stage 2/3 can ground themselves in a stable dossier even when SEAP
/// becomes temporarily unavailable or replaces a file upstream. Streaming a file into this store computes the
/// SHA-256 of the REAL stored bytes and returns the reference + content digest.
/// </summary>
public interface ISeapOriginalDocumentStore
{
    /// <summary>Streams <paramref name="content"/> into the dossier store under the tender's namespace and returns
    /// the persisted reference plus the content digest/size captured from the actual bytes. The caller owns and
    /// disposes the stream. Throws when the dossier storage is not configured; throws
    /// <see cref="SeapOriginalDocumentTooLargeException"/> when the file exceeds
    /// <c>Seap:MaxOriginalDocumentBytes</c> (no partial object is left behind).</summary>
    Task<StoredSeapOriginalDocument> StoreOriginalAsync(
        Guid tenderId, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens a durably stored original by its dossier reference. Returns null when the object does not
    /// exist. Used ONLY by the controlled dossier download route — the caller has already verified that the
    /// attachment belongs to the tender, so the reference is always a row this tender owns.</summary>
    Task<SeapOriginalDocumentContent?> OpenOriginalAsync(string reference, CancellationToken cancellationToken);

    /// <summary>Deletes a durably stored original object (used when a tender-level dossier quota is exceeded or a
    /// partially written object must be cleaned up after a failed/cancelled upload).</summary>
    Task DeleteOriginalAsync(string reference, CancellationToken cancellationToken);
}

public interface ISeapIngestionService
{
    Task<SeapTenderImportResult> ImportNoticeAsync(SeapTenderNoticeDto notice, CancellationToken cancellationToken);
    Task<int> PollAndImportAsync(CancellationToken cancellationToken);

    /// <summary>
    /// On-demand SEAP search (Anunțuri SEAP → „Caută în SEAP"): takes the active filter parameters
    /// (CPV prefixes + counties), pages the live e-licitatie.ro search — optionally restricted to a
    /// publication-date window (default: the current year) — derives each notice's county deterministically,
    /// imports the notices matching the parameters (no LLM categorization, no attachment downloads), and
    /// returns them. Because the anonymous search caps wide windows at ~3000 notices with an unstable slice,
    /// it additionally scans the trailing 30 days (clamped to the requested window) so recent matching
    /// notices are found reliably. Bypasses the background hourly import cap because it is a user-initiated action.
    /// </summary>
    Task<IReadOnlyCollection<SeapTender>> SearchAndImportAsync(
        string[]? cpvPrefixes, string[]? counties, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// „Direct hit" resolve by SEAP notice number (<c>SCN…</c>/<c>CN…</c>). Local first: a tender that
    /// already carries a document-grounded analysis is returned as ready (no network call). A local row that
    /// is not ready yet gets an ingestion run (no re-search — the numeric <c>NoticeId</c> is already known).
    /// Only on a genuine local miss is the live platform queried with an exact notice-number match; when the
    /// notice exists it is upserted by the unique normalized id (metadata + <c>NoticeId</c>) and an ingestion
    /// run is started — even when the deadline already passed (a <see cref="SeapImportMode.DirectHistoricalLookup"/>
    /// that is never surfaced as an active opportunity). Returns null when SEAP answered normally and has no
    /// such notice; throws <see cref="SeapSourceUnavailableException"/> when the platform is unreachable.
    /// Document download and the LLM analysis are deliberately NOT part of this call — they belong to the
    /// background ingestion worker and are tracked on the returned run.
    /// </summary>
    Task<SeapResolveResult?> ResolveNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken);

    /// <summary>Returns the current ingestion state of a tender (its latest run + the tender-level import
    /// markers). Null when the tender does not exist.</summary>
    Task<SeapIngestionStatusDto?> GetIngestionStatusAsync(Guid tenderId, CancellationToken cancellationToken);

    /// <summary>Worker tick entry point (Livrarea 2): claims and processes up to <paramref name="maxRuns"/>
    /// active runs whose lease is free/expired, advancing each through the persistent checkpoints
    /// (fetch documents → extract text → LLM analysis). Idempotent and restart-safe; every checkpoint is
    /// fenced on ClaimedBy + ClaimVersion, so a worker that lost its lease can never overwrite the run.</summary>
    Task<int> ProcessPendingIngestionRunsAsync(string workerId, int maxRuns, CancellationToken cancellationToken);

    /// <summary>Lease heartbeat: renews the lease of every run currently claimed by <paramref name="workerId"/>.
    /// Invoked periodically by the lease-heartbeat background worker so long document/OCR/LLM operations do
    /// not let the lease expire under a live worker. Returns the number of renewed runs.</summary>
    Task<int> RenewActiveLeasesAsync(string workerId, CancellationToken cancellationToken);
}

/// <summary>Outcome of the AI categorizer for a standardized CPV code.</summary>
public sealed record SeapCategorizationResult(Guid CategoryId, string CategoryName, bool NewCategory, bool FromCache);

/// <summary>
/// Resolves the CompaSpot category for a SEAP CPV code. CPV codes are standardized, so the persistent
/// <c>CpvCategoryMappings</c> cache is consulted first and the LLM is called at most once per unique code;
/// categories the LLM returns that do not exist yet are auto-created under the General Procurement root.
/// Every failure degrades to null — the tender still imports, uncategorized.
/// </summary>
public interface ISeapCategorizer
{
    Task<SeapCategorizationResult?> CategorizeAsync(
        string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken);
}

/// <summary>
/// AI summarizer for a SEAP tender: sends the available notice data (title, CPV, estimated value, buyer,
/// county, deadlines, description when present) — plus the text of already-processed attachments (Fișa de
/// Date / Caiet de Sarcini) when it has been read — to the shared <c>ILlmProvider</c>
/// and returns structured, decision-critical values („Sinteză AI" bullets, award criterion, participation
/// guarantee, funding source, duration, qualification requirements, key personnel, mandatory certificates).
/// Enrichment is never a
/// gate: every failure degrades to null and the tender still imports without AI details.
/// </summary>
public interface ISeapSummaryGenerator
{
    Task<SeapAiExtraction?> GenerateAsync(
        SeapTender tender, string? attachmentText, CancellationToken cancellationToken);
}

/// <summary>
/// Reads readable text (PDF text layer, DOCX, ZIP archives and plain text) from a SEAP tender's published
/// attachments — downloading remote e-licitatie.ro files on demand or reading the locally stored copy — so
/// the on-demand „Sinteză AI\" generation can be grounded in the original Fișa de Date / Caietul de Sarcini
/// content. Best-effort: an attachment that cannot be downloaded or yields no readable text is skipped;
/// returns null when no attachment produced readable text.
/// </summary>
/// <summary>Content digest of one published attachment, captured from the actual downloaded bytes.</summary>
public sealed record SeapAttachmentContentDigest(string FileName, string SourceUrl, string ContentSha256, long SizeBytes);

/// <summary>Outcome of a text-extraction pass over a tender's published attachments: the concatenated readable
/// text (null when no attachment yields text) plus the per-file SHA-256 content digests that let the worker
/// stamp content-based document hashes (a file replaced upstream keeping URL + name changes the digest).</summary>
public sealed record SeapAttachmentReadResult(
    string? Text,
    IReadOnlyCollection<SeapAttachmentContentDigest> Digests);

public interface ISeapAttachmentTextReader
{
    Task<SeapAttachmentReadResult> ReadPublishedTextAsync(
        IReadOnlyCollection<SeapTenderAttachment> attachments, CancellationToken cancellationToken);
}

/// <summary>Lightweight category reference exposed on the SEAP tender detail page.</summary>
public sealed record SeapTenderCategoryRef(Guid Id, string Name);

/// <summary>One original file published by the contracting authority. The <see cref="DownloadUrl"/> is the
/// authenticated API route that streams the stored file (browsers cannot navigate the persisted
/// <c>local://</c>/<c>s3://</c> references directly). When a <see cref="Marketplace.Domain.Seap.SeapIngestionRunPurpose.DocumentImport"/>
/// run already durably stored the file (<see cref="DownloadStatus"/> = Downloaded) the URL points at the
/// controlled dossier route (<c>…/documents/{attachmentId}/download</c>, which streams the exact stored
/// original bytes); otherwise it points at the browsing route that proxies the SEAP source or reads the
/// browsing copy. <see cref="DocumentGroup"/>/<see cref="DownloadStatus"/> carry the dossier classification
/// and the per-file durable-download state once a DocumentImport run processed the tender.</summary>
public sealed record SeapTenderAttachmentRef(
    Guid Id,
    string FileName,
    string DownloadUrl,
    string? DocumentGroup = null,
    string? DownloadStatus = null,
    /// <summary>Size of the published bytes, when the file was downloaded/stored (durably or as a digest).</summary>
    long? SizeBytes = null,
    /// <summary>Machine-readable failure code of the last durable-download attempt (e.g. DownloadTimeout,
    /// OriginalDocumentTooLarge); null unless the file is <c>Failed</c>.</summary>
    string? DownloadErrorCode = null);

/// <summary>
/// Detail projection of a SEAP tender returned by <c>GET /api/v1/seap/tenders/{id}</c>. Kept as a dedicated
/// DTO (instead of serializing the aggregate) so the page can evolve independently of persistence.
/// <c>ProcedureType</c> and <c>ProcedureState</c> are derived deterministically from the external id and the
/// deadline; the AI-extracted fields (award criterion, funding source, contract duration, guarantees,
/// qualification requirements, key personnel, mandatory certificates, summary bullets) carry real values
/// once the AI summarizer ran (at import or on demand) and are null before that — the SPA shows a
/// skeleton/placeholders until then. <c>SummaryDocumentNote</c> is a transient per-response value set only by
/// <c>POST /api/v1/seap/tenders/{id}/summary</c> when the synthesis had to fall back to notice metadata
/// because the published documents could not be downloaded/read automatically.
/// </summary>
public sealed record SeapTenderDetailsDto(
    Guid Id,
    string SeapExternalId,
    string Title,
    string? Description,
    IReadOnlyCollection<string> CpvCodes,
    IReadOnlyCollection<string> CpvNames,
    string? County,
    Guid? CategoryId,
    SeapTenderCategoryRef? Category,
    decimal? EstimatedValue,
    string? Currency,
    string BuyerName,
    DateTimeOffset PublicationDate,
    DateTimeOffset Deadline,
    string Status,
    string? ProposalTitle,
    string? TechnicalProposalUrl,
    string? BillOfQuantitiesUrl,
    IReadOnlyCollection<SeapTenderAttachmentRef> Attachments,
    string ProcedureType,
    string ProcedureState,
    string? AwardCriterion,
    string? FundingSource,
    string? ContractDuration,
    string? ParticipationGuarantee,
    IReadOnlyCollection<string>? QualificationRequirements,
    IReadOnlyCollection<string>? KeyPersonnel,
    IReadOnlyCollection<string>? Certificates,
    IReadOnlyCollection<string>? SummaryPoints,
    int? NoticeId,
    bool SummaryGroundedInDocuments,
    string? SummaryDocumentNote = null)
{
    /// <summary>Maps the aggregate to the detail projection. The SEAP external id distinguishes the simplified
    /// procedure ("SCN" = „Procedură simplificată") from a standard contract notice ("CN" = „Licitare deschisă");
    /// the procedure state is derived from the tender deadline („În desfășurare" while accepting offers).
    /// </summary>
    public static SeapTenderDetailsDto From(SeapTender tender, DateTimeOffset now) => new(
        tender.Id,
        tender.SeapExternalId,
        tender.Title,
        tender.Description,
        tender.CpvCodes,
        tender.CpvNames,
        tender.County,
        tender.CategoryId,
        tender.Category is null ? null : new SeapTenderCategoryRef(tender.Category.Id, tender.Category.Name),
        tender.EstimatedValue,
        tender.Currency,
        tender.BuyerName,
        tender.PublicationDate,
        tender.Deadline,
        tender.Status.ToString(),
        tender.ProposalTitle,
        tender.TechnicalProposalUrl,
        tender.BillOfQuantitiesUrl,
        tender.Attachments
            .Select(attachment => new SeapTenderAttachmentRef(
                attachment.Id,
                attachment.FileName,
                // A durably stored dossier original is always streamed through the controlled dossier route
                // (exact stored bytes, original file name/content type); everything else goes through the
                // browsing route (SEAP proxy or browsing copy).
                attachment.DownloadStatus == SeapDocumentDownloadStatus.Downloaded
                    ? $"api/v1/seap/tenders/{tender.Id}/documents/{attachment.Id}/download"
                    : $"api/v1/seap/tenders/{tender.Id}/attachments/{attachment.Id}",
                attachment.DocumentGroup == SeapDocumentGroup.None ? null : attachment.DocumentGroup.ToString(),
                attachment.DownloadStatus == SeapDocumentDownloadStatus.None ? null : attachment.DownloadStatus.ToString(),
                attachment.SizeBytes,
                attachment.DownloadErrorCode))
            .ToArray(),
        ProcedureType: tender.SeapExternalId.StartsWith("SCN", StringComparison.OrdinalIgnoreCase)
            ? "Procedură simplificată"
            : "Licitare deschisă",
        ProcedureState: ProcedureStateLabel(tender, now),
        AwardCriterion: tender.AwardCriterion,
        FundingSource: tender.FundingSource,
        ContractDuration: tender.ContractDuration,
        ParticipationGuarantee: tender.ParticipationGuarantee,
        QualificationRequirements: tender.QualificationRequirements is null ? null : [tender.QualificationRequirements],
        KeyPersonnel: tender.KeyPersonnel is null ? null : [tender.KeyPersonnel],
        Certificates: tender.Certificates is null ? null : [tender.Certificates],
        SummaryPoints: tender.SummaryPoints.Count > 0 ? tender.SummaryPoints : null,
        NoticeId: tender.NoticeId,
        SummaryGroundedInDocuments: tender.SummaryGroundedInDocuments);

    /// <summary>Maps the stored SEAP procedure-state text to the UI label. SEAP writes Romanian without
    /// diacritics („Anulata", „Atribuita", „In derulare"); a known state always wins over the deadline
    /// heuristic — an annulled/awarded procedure must never look „În desfășurare" merely because its
    /// deadline is still in the future. Unknown/absent states fall back to the deadline rule.</summary>
    private static string ProcedureStateLabel(SeapTender tender, DateTimeOffset now)
    {
        string? raw = tender.ProcedureStateText;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            string folded = FoldDiacritics(raw).ToLowerInvariant();
            if (folded.Contains("anulat", StringComparison.Ordinal)) return "Anulată";
            if (folded.Contains("atribuit", StringComparison.Ordinal)) return "Atribuită";
            if (folded.Contains("incheiat", StringComparison.Ordinal)
                || folded.Contains("finalizat", StringComparison.Ordinal)) return "Încheiată";
            if (folded.Contains("derulare", StringComparison.Ordinal)
                || folded.Contains("desfasurare", StringComparison.Ordinal)
                || folded.Contains("deschis", StringComparison.Ordinal)
                || folded.Contains("publicat", StringComparison.Ordinal)
                || folded.Contains("in curs", StringComparison.Ordinal)) return "În desfășurare";
        }
        return tender.Deadline >= now ? "În desfășurare" : "Încheiată";
    }

    private static string FoldDiacritics(string value) =>
        value.Replace("ă", "a", StringComparison.Ordinal)
            .Replace("â", "a", StringComparison.Ordinal)
            .Replace("î", "i", StringComparison.Ordinal)
            .Replace("ș", "s", StringComparison.Ordinal)
            .Replace("ş", "s", StringComparison.Ordinal)
            .Replace("ț", "t", StringComparison.Ordinal)
            .Replace("ţ", "t", StringComparison.Ordinal);
}
