using Marketplace.Application.Bidding;
using Marketplace.Application.Companies;
using Marketplace.Domain.Qualification;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.AutoBidding;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using System.Globalization;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>
/// Orchestrates: embed tender requirements → pgvector RAG over CompanyVault chunks → LLM proposal →
/// parse JSON → .docx/.xlsx → S3. Shadow Mode only affects visibility/charging (handled by the API),
/// never the generation itself.
/// </summary>
public sealed partial class BiddingGenerationService : IBiddingGenerationService
{
    private const string RomanianProcurementExpertSystemPrompt =
        "Ești un expert român în achiziții publice. Analizează cerințele anunțului SEAP și datele firmei " +
        "și generează o propunere tehnică și o listă de cantități. " +
        "Răspunde DOAR cu JSON valid, în limba română, fără text înainte sau după, cu structura: " +
        "{\"title\": string, \"technicalProposal\": {\"sections\": [{\"heading\": string, \"content\": string}]}, " +
        "\"billOfQuantities\": [{\"position\": int, \"description\": string, \"unit\": string, " +
        "\"quantity\": number, \"unitPrice\": number, \"total\": number}], " +
        "\"financialSummary\": [{\"label\": string, \"value\": number, \"note\": string}]}. " +
        "Lista de cantități trebuie să conțină pozițiile relevante pentru lucrările/achizia descrisă în anunț. " +
        "financialSummary: dacă anunțul nu conține o recapitulație financiară, returnează []. " +
        "Nu inventa certificări, prețuri sau capacități care nu sunt susținute de datele furnizate. " +
        "Reguli pentru datele firmei din blocul „Date de calificare ale firmei”: " +
        "1) În formularele administrative (reprezentant legal, date de contact, cont IBAN) folosește DOAR datele reale din bloc; " +
        "dacă o dată lipsește, scrie exact „[De completat: <denumirea câmpului>]”. " +
        "2) Pentru cerințele de experiență similară din caietul de sarcini, potrivește cel mai adecvat contract din lista de experiență " +
        "și referă-l explicit (client, titlu, cod CPV, valoare). " +
        "3) În secțiunea de resurse umane nominalizează DOAR experții enumerați în bloc (nume, rol, calificări); " +
        "dacă nu există experți, scrie „[De completat: Expert cheie]”. " +
        "4) Nu inventa nicio dată de firmă (CUI, IBAN, contract, certificat, expert) care nu apare explicit în bloc.";

    private readonly MarketplaceDbContext dbContext;
    private readonly ILlmProvider llmProvider;
    private readonly ICompanyQualificationService qualificationService;
    private readonly IDocumentGenerator documentGenerator;
    private readonly IEmbeddingService embeddingService;
    private readonly IStorageService storage;
    private readonly bool useLocalStorage;
    private readonly AutoBiddingOptions settings;
    private readonly TimeProvider timeProvider;
    private readonly IPipelineProgressStore? progressStore;
    private readonly ILogger<BiddingGenerationService> logger;

    public BiddingGenerationService(
        MarketplaceDbContext dbContext,
        ILlmProvider llmProvider,
        ICompanyQualificationService qualificationService,
        IDocumentGenerator documentGenerator,
        IEmbeddingService embeddingService,
        IOptions<AutoBiddingOptions> options,
        IOptions<LocalStorageOptions> localStorage,
        IStorageService storage,
        TimeProvider timeProvider,
        ILogger<BiddingGenerationService> logger,
        IPipelineProgressStore? progressStore = null)
    {
        this.dbContext = dbContext;
        this.llmProvider = llmProvider;
        this.qualificationService = qualificationService;
        this.documentGenerator = documentGenerator;
        this.embeddingService = embeddingService;
        this.storage = storage;
        useLocalStorage = localStorage.Value.UseLocalStorage;
        settings = options.Value;
        this.timeProvider = timeProvider;
        this.progressStore = progressStore;
        this.logger = logger;
    }


    public async Task<BidGenerationResult> GenerateForTenderAsync(
        Guid? companyId, Guid tenderId, CancellationToken cancellationToken, Guid? progressRequestId = null)
    {
        SeapTender? tender = await dbContext.SeapTenders.Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken)
            ?? throw new KeyNotFoundException("Anunțul SEAP nu există.");

        string requirements = BuildRequirementsText(tender);
        string vaultContext = await BuildVaultContextAsync(requirements, cancellationToken);
        string qualificationContext = await BuildQualificationContextAsync(companyId, cancellationToken);

        string userPrompt = string.IsNullOrWhiteSpace(vaultContext)
            ? $"Cerințele anunțului:\n{requirements}\n\n{qualificationContext}\n\nDate despre firmă (fragmente relevante din CompanyVault): (nu sunt disponibile documente în CompanyVault)."
            : $"Cerințele anunțului:\n{requirements}\n\n{qualificationContext}\n\nDate despre firmă (fragmente relevante din CompanyVault):\n{vaultContext}";
        if (progressRequestId is Guid generatingId && progressStore is not null)
            progressStore.Report(generatingId, PipelineStage.Generating, "Claude redactează propunerea tehnică…");
        string llmJson = await llmProvider.CompleteAsync(RomanianProcurementExpertSystemPrompt, userPrompt, cancellationToken);
        BiddingProposal proposal = BiddingJsonParser.Parse(llmJson);
        if (progressRequestId is Guid finalizingId && progressStore is not null)
            progressStore.Report(finalizingId, PipelineStage.Finalizing, "Generăm documentele .docx și .xlsx…");

        GeneratedDocument technical = await documentGenerator.GenerateTechnicalProposalAsync(proposal, cancellationToken);
        GeneratedDocument boq = await documentGenerator.GenerateBillOfQuantitiesAsync(proposal, cancellationToken);
        string technicalUrl = await UploadDocumentAsync(tender.Id, technical, cancellationToken);
        string boqUrl = await UploadDocumentAsync(tender.Id, boq, cancellationToken);

        tender.RecordGeneratedBid(proposal.Title, technicalUrl, boqUrl, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        LogBidGenerated(logger, tender.Id, settings.IsShadowMode);
        return new(tender.Id, proposal.Title, technicalUrl, boqUrl, settings.IsShadowMode, timeProvider.GetUtcNow());
    }

    public async Task<IReadOnlyCollection<SeapTender>> ListTendersAsync(
        Guid[]? categoryIds, int? radiusKm, string? supplierCounty, CancellationToken cancellationToken,
        string[]? cpvPrefixes = null, string[]? counties = null, SeapTenderStatus? status = null)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        // Only procedurally open tenders (the submission deadline is still in the future) belong in the
        // discovery feed — closed procedures cannot be bid on and would only clutter the list. The
        // generated-offers history (BidGenerated) is the company's activity log, so it keeps closed
        // tenders too (an offer generated while the tender was open stays reachable after the deadline).
        bool includeExpired = status == SeapTenderStatus.BidGenerated;
        // The list never carries attachments (only the detail projection needs them) — keeps the feed
        // response lean as the local SEAP cache grows.
        IQueryable<SeapTender> query = dbContext.SeapTenders.AsNoTracking()
            .Include(item => item.Category)
            .Where(tender => includeExpired || tender.Deadline >= now);
        if (status is not null)
            query = query.Where(tender => tender.Status == status);

        if (categoryIds is { Length: > 0 })
        {
            string[] categoryCpvPrefixes = await dbContext.Categories.AsNoTracking()
                .Where(category => categoryIds.Contains(category.Id))
                .SelectMany(category => category.CpvPrefixes)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            if (categoryCpvPrefixes.Length > 0)
            {
                // The AI-mapped CategoryId is authoritative; the CPV-prefix match is the fallback for
                // tenders that are not yet categorized (LLM unavailable or code not seen before).
                Guid[] prefixMatchedIds = await FindCpvPrefixMatchedTenderIdsAsync(
                    categoryCpvPrefixes, status, includeExpired, now, cancellationToken);
                query = query.Where(tender =>
                    (tender.CategoryId != null && categoryIds.Contains(tender.CategoryId.Value)) ||
                    (tender.CategoryId == null && prefixMatchedIds.Contains(tender.Id)));
            }
            else
            {
                // No CPV prefixes configured on the selected categories (e.g. "Achiziții generale").
                query = query.Where(tender => tender.CategoryId != null && categoryIds.Contains(tender.CategoryId.Value));
            }
        }

        if (radiusKm is > 0)
        {
            if (string.IsNullOrWhiteSpace(supplierCounty))
            {
                LogRadiusFilterSkipped(logger, radiusKm.Value);
            }
            else
            {
                // County-seat-to-county-seat distance. Tenders without a recognizable county are excluded
                // only while the radius filter is explicitly active.
                var knownCountyIds = await dbContext.SeapTenders.AsNoTracking()
                    .Where(tender => tender.County != null)
                    .Select(tender => new { tender.Id, tender.County })
                    .ToListAsync(cancellationToken);
                HashSet<Guid> allowedIds = knownCountyIds
                    .Where(item => item.County is not null &&
                        CountyDistance.KilometersBetween(supplierCounty, item.County) is double distance &&
                        distance <= radiusKm.Value)
                    .Select(item => item.Id)
                    .ToHashSet();
                query = query.Where(tender => allowedIds.Contains(tender.Id));
            }
        }

        // SEAP Tender Match profile filters: deterministic CPV-prefix match over the tender's own CPV codes
        // (applies to categorized and uncategorized tenders alike) and an exact canonical-county match.
        if (cpvPrefixes is { Length: > 0 })
        {
            string[] cleanPrefixes = cpvPrefixes
                .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (cleanPrefixes.Length > 0)
            {
                Guid[] cpvMatchedIds = await FindCpvPrefixMatchedTenderIdsAsync(
                    cleanPrefixes, status, includeExpired, now, cancellationToken);
                query = query.Where(tender => cpvMatchedIds.Contains(tender.Id));
            }
        }

        if (counties is { Length: > 0 })
        {
            string[] canonicalCounties = counties
                .Select(RomanianCountyCodes.TryNormalize)
                .Where(county => county is not null).Select(county => county!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (canonicalCounties.Length > 0)
                query = query.Where(tender => tender.County != null && canonicalCounties.Contains(tender.County));
        }

        return await query.OrderByDescending(item => item.PublicationDate).ToArrayAsync(cancellationToken);
    }

    /// <summary>Ids of tenders whose CPV codes start with any of the given prefixes. The same base filters as
    /// the feed (deadline + status) are pushed into the raw query so the <c>LIKE ANY</c> scan never walks the
    /// whole local cache table — only the open (or history) tenders that could match the feed.</summary>
    private async Task<Guid[]> FindCpvPrefixMatchedTenderIdsAsync(
        string[] prefixes, SeapTenderStatus? status, bool includeExpired, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string[] likePatterns = prefixes.Select(CpvPrefix.ToMatchPattern).ToArray();
        // NpgsqlParameter with an explicit array type: a bare string[] argument is sent as a
        // scalar text and `LIKE ANY (CAST(...))` then fails to parse the value as an array.
        NpgsqlParameter patterns = new()
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text,
            Value = likePatterns
        };
        List<NpgsqlParameter> parameters = [patterns];
        var sql = new System.Text.StringBuilder(
            "SELECT t.\"Id\" FROM \"SeapTenders\" t " +
            "WHERE EXISTS (SELECT 1 FROM unnest(t.\"CpvCodes\") cpv WHERE cpv LIKE ANY ({0}))");
        int index = 1;
        if (!includeExpired)
        {
            parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = now });
            sql.Append(CultureInfo.InvariantCulture, $" AND t.\"Deadline\" >= {{{index++}}}");
        }
        if (status is not null)
        {
            parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = status.Value.ToString() });
            sql.Append(CultureInfo.InvariantCulture, $" AND t.\"Status\" = {{{index++}}}");
        }
        return await dbContext.Database.SqlQueryRaw<Guid>(sql.ToString(), parameters.ToArray())
            .ToArrayAsync(cancellationToken);
    }

    public Task<SeapTender?> GetTenderAsync(Guid tenderId, CancellationToken cancellationToken) =>
        dbContext.SeapTenders.AsNoTracking()
            .Include(item => item.Attachments)
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken);

    private static string BuildRequirementsText(SeapTender tender)
    {
        List<string> parts = [tender.Title];
        if (!string.IsNullOrWhiteSpace(tender.Description)) parts.Add(tender.Description);
        if (tender.CpvCodes.Count > 0) parts.Add($"Coduri CPV: {string.Join(", ", tender.CpvCodes)}");
        if (tender.EstimatedValue.HasValue) parts.Add($"Valoare estimată: {tender.EstimatedValue.Value:N2} {tender.Currency}");
        parts.Add($"Instituție contractantă: {tender.BuyerName}");
        parts.Add($"Termen depunere: {tender.Deadline:dd.MM.yyyy}");
        return string.Join("\n", parts);
    }

    /// <summary>
    /// RAG over CompanyVault chunks. When embeddings are not configured (no OpenAI key), the flow
    /// degrades to a plain LLM generation without company context instead of failing the request —
    /// mirroring CompanyVaultService.TryEmbedAsync.
    /// </summary>
    private async Task<string> BuildVaultContextAsync(string requirements, CancellationToken cancellationToken)
    {
        try
        {
            float[] queryEmbedding = await embeddingService.EmbedAsync(requirements, cancellationToken);
            Vector vector = new(queryEmbedding);
            List<DocumentChunk> chunks = await dbContext.DocumentChunks
                .FromSqlInterpolated($"""
                    SELECT * FROM "DocumentChunks"
                    ORDER BY "Embedding" <=> {vector}
                    LIMIT {Math.Max(1, settings.TopChunks)}
                    """)
                .ToListAsync(cancellationToken);
            return string.Join("\n\n", chunks.Select(chunk => chunk.Content));
        }
        catch (InvalidOperationException exception)
        {
            LogEmbeddingsUnavailable(logger, exception.Message);
            return string.Empty;
        }
    }

    /// <summary>Fallback block used when no company (or no qualification data) is available: the generation
    /// never blocks, but the LLM is told to fill any company data required by the forms with clearly marked
    /// „[De completat: …]" placeholders instead of inventing values.</summary>
    private const string NoQualificationFallback =
        "Date de calificare ale firmei: (nu sunt disponibile în Vault-ul de calificare) — folosește placeholdere marcate " +
        "„[De completat: …]” pentru orice dată de firmă cerută de formulare, fără a inventa valori.";

    /// <summary>
    /// Builds the „Date de calificare ale firmei" context block for the LLM from the requesting company's
    /// SEAP qualification vault. When no company is known (e.g. the post-checkout webhook path) or the vault
    /// has no data, the non-gate fallback is returned — generation always proceeds.
    /// </summary>
    private async Task<string> BuildQualificationContextAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is not Guid id) return NoQualificationFallback;
        CompanyQualificationDto? qualification = await qualificationService.GetQualificationAsync(id, cancellationToken);
        if (qualification is null) return NoQualificationFallback;

        List<string> lines = ["Date de calificare ale firmei (din Vault-ul de calificare):"];
        if (qualification.Legal is { } legal)
        {
            lines.Add($"Reprezentant legal: {legal.LegalRepresentativeName}" +
                (string.IsNullOrWhiteSpace(legal.LegalRepresentativeRole) ? "" : $" ({legal.LegalRepresentativeRole})"));
            if (!string.IsNullOrWhiteSpace(legal.LegalForm)) lines.Add($"Formă juridică: {legal.LegalForm}");
            foreach (BankAccountDto account in legal.BankAccounts)
                lines.Add($"Cont IBAN: {account.BankName} {account.Iban} ({account.Currency})");
        }
        if (qualification.Experience.Count > 0)
        {
            lines.Add("Experiență similară:");
            foreach (CompanyExperienceDto item in qualification.Experience)
            {
                List<string> parts = [$"{item.ClientName} — {item.ContractTitle}"];
                if (item.ValueAmount is { } value) parts.Add($"valoare {value:N2} {item.ValueCurrency}");
                if (!string.IsNullOrWhiteSpace(item.CpvCode)) parts.Add($"CPV {item.CpvCode}");
                if (item.CompletionDate is { } date) parts.Add($"finalizat {date:dd.MM.yyyy}");
                lines.Add($"- {string.Join(", ", parts)}");
            }
        }
        if (qualification.Certificates.Count > 0)
        {
            lines.Add("Certificate:");
            foreach (CompanyCertificateDto certificate in qualification.Certificates)
            {
                List<string> parts = [CertificateTypeLabel(certificate.CertificateType)];
                if (!string.IsNullOrWhiteSpace(certificate.Issuer)) parts.Add($"emis de {certificate.Issuer}");
                if (certificate.ValidUntil is { } valid) parts.Add($"valabil până la {valid:dd.MM.yyyy}");
                lines.Add($"- {string.Join(", ", parts)}");
            }
        }
        if (qualification.Personnel.Count > 0)
        {
            lines.Add("Experți cheie:");
            foreach (CompanyPersonnelDto expert in qualification.Personnel)
            {
                string qualifications = expert.ProfessionalCertificates.Count > 0
                    ? $" ({string.Join(", ", expert.ProfessionalCertificates)})"
                    : "";
                lines.Add($"- {expert.FullName} — {expert.Role}{qualifications}");
            }
        }
        return lines.Count > 1 ? string.Join("\n", lines) : NoQualificationFallback;
    }

    private static string CertificateTypeLabel(CompanyCertificateType type) => type switch
    {
        CompanyCertificateType.Iso9001 => "ISO 9001",
        CompanyCertificateType.Iso14001 => "ISO 14001",
        CompanyCertificateType.Iso45001 => "ISO 45001",
        CompanyCertificateType.Anre => "ANRE",
        CompanyCertificateType.Cnsas => "CNSAS",
        _ => "Alt certificat"
    };

    private async Task<string> UploadDocumentAsync(Guid tenderId, GeneratedDocument document, CancellationToken cancellationToken)
    {
        if (useLocalStorage)
            return await storage.UploadAsync("bids", $"bids/{tenderId}", document.FileName,
                new MemoryStream(document.Content), document.ContentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DocumentsBucket))
            return $"memory://bids/{tenderId}/{document.FileName}";
        return await storage.UploadAsync(settings.DocumentsBucket, $"bids/{tenderId}", document.FileName,
            new MemoryStream(document.Content), document.ContentType, cancellationToken);
    }

    [LoggerMessage(EventId = 5301, Level = LogLevel.Warning, Message = "Embeddings unavailable ({Reason}); generating the bid without CompanyVault context.")]
    private static partial void LogEmbeddingsUnavailable(ILogger logger, string reason);
    [LoggerMessage(EventId = 5302, Level = LogLevel.Information, Message = "Auto-bid generated for SEAP tender {TenderId} (shadow mode: {ShadowMode}).")]
    private static partial void LogBidGenerated(ILogger logger, Guid tenderId, bool shadowMode);
    [LoggerMessage(EventId = 5303, Level = LogLevel.Warning, Message = "Radius filter ({RadiusKm} km) skipped: the supplier profile has no county to measure from.")]
    private static partial void LogRadiusFilterSkipped(ILogger logger, int radiusKm);
}

