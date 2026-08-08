using System.Text.Json;
using System.Text.Json.Serialization;
using Marketplace.Application.AiIntake;
using Marketplace.Application.Rfqs;
using Marketplace.Application.Operations;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.AiIntake;

internal sealed partial class AssistedIntakeService(
    MarketplaceDbContext dbContext,
    INeedExtractionProvider extractionProvider,
    IRfqService rfqService,
    TimeProvider timeProvider,
    IOptions<AiIntakeOptions> options,
    ILogger<AssistedIntakeService> logger,
    IProductEventService productEvents) : IAssistedIntakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly AiIntakeOptions settings = options.Value;

    public AssistedIntakeAvailability Availability => new(settings.Enabled, extractionProvider.IsAvailable);

    public async Task<AssistedIntakeResult> StartAsync(Guid buyerId, string input, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureAiAvailable();
        DateTimeOffset now = timeProvider.GetUtcNow();
        AiIntakeSession session = AiIntakeSession.Create(Guid.NewGuid(), buyerId, input, now);
        NeedExtractionContext context = await BuildContextAsync(session, [], null, now, cancellationToken);
        dbContext.AiIntakeSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        NeedExtractionOutcome outcome;
        try { outcome = await ExtractAsync(context, cancellationToken); }
        catch (AiIntakeUnavailableException)
        {
            productEvents.Record("AiIntakeFailed", "AiIntakeSession", session.Id, buyerId, null, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        Apply(session, [], outcome, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(session, outcome);
    }

    public async Task<AssistedIntakeResult?> GetAsync(Guid buyerId, Guid sessionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        AiIntakeSession? session = await dbContext.AiIntakeSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == buyerId, cancellationToken);
        return session is null ? null : ToResult(session, DeserializeOutcome(session.ExtractionJson));
    }

    public async Task<AssistedIntakeResult?> ClarifyAsync(
        Guid buyerId, Guid sessionId, string answer, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureAiAvailable();
        string cleanAnswer = answer?.Trim() ?? string.Empty;
        if (cleanAnswer.Length is < 1 or > 1000)
            throw new AiIntakeRuleException("Răspunsul trebuie să aibă între 1 și 1.000 de caractere.");

        AiIntakeSession? session = await dbContext.AiIntakeSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == buyerId, cancellationToken);
        if (session is null) return null;
        NeedExtractionOutcome previous = DeserializeOutcome(session.ExtractionJson);
        if (previous.Clarification is null)
            throw new AiIntakeRuleException("Draftul nu mai are o clarificare în așteptare.");

        List<ClarificationTurn> turns = DeserializeTurns(session.TranscriptJson);
        turns.Add(new(previous.Clarification.Field, previous.Clarification.Question, cleanAnswer));
        DateTimeOffset now = timeProvider.GetUtcNow();
        NeedExtractionOutcome outcome;
        try { outcome = await ExtractAsync(await BuildContextAsync(session, turns, previous.Draft, now, cancellationToken), cancellationToken); }
        catch (AiIntakeUnavailableException)
        {
            productEvents.Record("AiIntakeFailed", "AiIntakeSession", session.Id, buyerId, null, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        Apply(session, turns, outcome, now);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new AiIntakeConcurrencyException("Draftul asistat a fost modificat într-o altă fereastră. Reîncarcă pagina.", exception);
        }
        return ToResult(session, outcome);
    }

    public async Task<Rfq?> ApproveAsync(
        Guid buyerId, Guid sessionId, RfqDraft approvedDraft, bool completedInAdvancedForm,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        Rfq? result = null;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            AiIntakeSession? session = await dbContext.AiIntakeSessions
                .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == buyerId, cancellationToken);
            if (session is null) return;
            if (session.ApprovedRfqId is Guid existingId)
            {
                result = await rfqService.GetAsync(buyerId, existingId, cancellationToken);
                return;
            }

            NeedExtractionOutcome extraction = DeserializeOutcome(session.ExtractionJson);
            List<ClarificationTurn> turns = DeserializeTurns(session.TranscriptJson);
            if (completedInAdvancedForm)
                session.CompleteInAdvancedForm(timeProvider.GetUtcNow());
            var assistedDraft = approvedDraft with
            {
                Source = ProcurementNeedSource.Assisted,
                InputType = extraction.Draft.InputType,
                BuyerNotes = AppendClarifications(approvedDraft.BuyerNotes, turns)
            };
            result = await rfqService.CreateAsync(buyerId, assistedDraft, cancellationToken);
            session.Approve(result.Id, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return result;
    }

    private async Task<NeedExtractionContext> BuildContextAsync(
        AiIntakeSession session, IReadOnlyCollection<ClarificationTurn> turns, AssistedNeedDraft? previous,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var buyer = await dbContext.BuyerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.BuyerId, cancellationToken)
            ?? throw new AiIntakeRuleException("Completează profilul de cumpărător înainte de a crea o cerere.");
        IntakeCategory[] categories = await dbContext.Categories.AsNoTracking()
            .Where(item => item.IsActive).OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new IntakeCategory(item.Id, item.Name, item.Slug)).ToArrayAsync(cancellationToken);
        return new(session.BuyerId, session.InitialInput, buyer.DefaultCity, buyer.DefaultCounty,
            buyer.MarketRadiusKm, categories, turns, previous, now);
    }

    private async Task<NeedExtractionOutcome> ExtractAsync(NeedExtractionContext context, CancellationToken cancellationToken)
    {
        EnsureAiAvailable();
        try
        {
            return await extractionProvider.ExtractAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AiIntakeUnavailableException)
        {
            LogProviderUnavailable(logger, exception);
            throw new AiIntakeUnavailableException(
                "Poți continua aceeași cerere în formularul complet; informațiile deja salvate vor fi păstrate.", exception);
        }
    }

    private static void Apply(AiIntakeSession session, IReadOnlyCollection<ClarificationTurn> turns,
        NeedExtractionOutcome outcome, DateTimeOffset now) => session.ApplyExtraction(
            JsonSerializer.Serialize(turns, JsonOptions), JsonSerializer.Serialize(outcome, JsonOptions),
            outcome.Provider, outcome.Model, outcome.PromptVersion, outcome.InputTokens, outcome.OutputTokens,
            outcome.Clarification is not null, now);

    private static NeedExtractionOutcome DeserializeOutcome(string json) =>
        JsonSerializer.Deserialize<NeedExtractionOutcome>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored assisted-intake extraction is invalid.");

    private static List<ClarificationTurn> DeserializeTurns(string json) =>
        JsonSerializer.Deserialize<List<ClarificationTurn>>(json, JsonOptions) ?? [];

    private static AssistedIntakeResult ToResult(AiIntakeSession session, NeedExtractionOutcome outcome) => new(
        session.Id, session.InitialInput, session.Status, outcome.Mode, session.Provider, session.Model, session.PromptVersion,
        outcome.Draft, outcome.Assumptions, outcome.Uncertainties, outcome.ResearchSources ?? [],
        outcome.Clarification,
        DeserializeTurns(session.TranscriptJson).Select(turn =>
            new AssistedIntakeAnswer(turn.Field, turn.Question, turn.Answer)).ToArray(),
        session.InputTokens, session.OutputTokens, session.ApprovedRfqId);

    private static string? AppendClarifications(string? buyerNotes, List<ClarificationTurn> turns)
    {
        if (turns.Count == 0) return buyerNotes;
        const string heading = "Clarificări din asistent:";
        string cleanNotes = buyerNotes?.Trim() ?? string.Empty;
        if (cleanNotes.Contains(heading, StringComparison.Ordinal)) return cleanNotes;
        string transcript = string.Join("\n", turns.Select(turn => $"Î: {turn.Question}\nR: {turn.Answer}"));
        string combined = string.IsNullOrWhiteSpace(cleanNotes)
            ? $"{heading}\n{transcript}"
            : $"{cleanNotes}\n\n{heading}\n{transcript}";
        return combined.Length <= 2000 ? combined : combined[..2000];
    }

    private void EnsureEnabled()
    {
        if (!settings.Enabled) throw new AiIntakeRuleException("Crearea asistată este momentan indisponibilă.");
    }

    private void EnsureAiAvailable()
    {
        if (!extractionProvider.IsAvailable)
            throw new AiIntakeUnavailableException("Continuă cu formularul complet pentru a crea cererea.");
    }

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning,
        Message = "AI intake provider failed; the client will continue in the advanced form.")]
    private static partial void LogProviderUnavailable(ILogger logger, Exception exception);
}
