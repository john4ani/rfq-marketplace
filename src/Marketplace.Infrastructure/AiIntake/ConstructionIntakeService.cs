using System.Text.Json;
using System.Text.Json.Serialization;
using Marketplace.Application.AiIntake;
using Marketplace.Application.Operations;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.AiIntake;

// Construction Projects intake: a distinct interpretation workflow that reuses the generic AiIntakeSession storage and,
// after human confirmation, derives one shared Procurement Need through the same Procurement Core as General (Task 10).
internal sealed partial class ConstructionIntakeService(
    MarketplaceDbContext dbContext,
    IConstructionProjectInterpreter interpreter,
    IRfqService rfqService,
    TimeProvider timeProvider,
    IOptions<AiIntakeOptions> options,
    ILogger<ConstructionIntakeService> logger,
    IProductEventService productEvents) : IConstructionIntakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly AiIntakeOptions settings = options.Value;

    public AssistedIntakeAvailability Availability => new(settings.Enabled, interpreter.IsAvailable);

    public async Task<ConstructionIntakeResult> StartAsync(Guid userId, string input, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureAiAvailable();
        DateTimeOffset now = timeProvider.GetUtcNow();
        AiIntakeSession session = AiIntakeSession.Create(Guid.NewGuid(), userId, input, now, AiIntakeKind.Construction);
        ConstructionProjectInterpretationRequest context = await BuildContextAsync(session, [], null, now, cancellationToken);
        dbContext.AiIntakeSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        ConstructionProjectInterpretation outcome;
        try { outcome = await InterpretAsync(context, cancellationToken); }
        catch (AiIntakeUnavailableException)
        {
            productEvents.Record("ConstructionIntakeFailed", "AiIntakeSession", session.Id, userId, null, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        Apply(session, [], outcome, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(session, outcome);
    }

    public async Task<ConstructionIntakeResult?> GetAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        AiIntakeSession? session = await dbContext.AiIntakeSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == userId && item.Kind == AiIntakeKind.Construction, cancellationToken);
        return session is null ? null : ToResult(session, DeserializeOutcome(session.ExtractionJson));
    }

    public async Task<ConstructionIntakeResult?> ClarifyAsync(
        Guid userId, Guid sessionId, string answer, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureAiAvailable();
        string cleanAnswer = answer?.Trim() ?? string.Empty;
        if (cleanAnswer.Length is < 1 or > 1000)
            throw new AiIntakeRuleException("Răspunsul trebuie să aibă între 1 și 1.000 de caractere.");

        AiIntakeSession? session = await dbContext.AiIntakeSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == userId && item.Kind == AiIntakeKind.Construction, cancellationToken);
        if (session is null) return null;
        ConstructionProjectInterpretation previous = DeserializeOutcome(session.ExtractionJson);
        if (previous.Clarification is null)
            throw new AiIntakeRuleException("Proiectul nu mai are o clarificare în așteptare.");

        List<ClarificationTurn> turns = DeserializeTurns(session.TranscriptJson);
        turns.Add(new(previous.Clarification.Field, previous.Clarification.Question, cleanAnswer));
        DateTimeOffset now = timeProvider.GetUtcNow();
        ConstructionProjectInterpretation outcome;
        try { outcome = await InterpretAsync(await BuildContextAsync(session, turns, previous.Draft, now, cancellationToken), cancellationToken); }
        catch (AiIntakeUnavailableException)
        {
            productEvents.Record("ConstructionIntakeFailed", "AiIntakeSession", session.Id, userId, null, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
        Apply(session, turns, outcome, now);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new AiIntakeConcurrencyException("Proiectul a fost modificat într-o altă fereastră. Reîncarcă pagina.", exception);
        }
        return ToResult(session, outcome);
    }

    public async Task<Rfq?> ApproveAsync(
        Guid userId, Guid sessionId, RfqDraft approvedDraft, bool completedInAdvancedForm,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        Rfq? result = null;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            AiIntakeSession? session = await dbContext.AiIntakeSessions
                .SingleOrDefaultAsync(item => item.Id == sessionId && item.BuyerId == userId && item.Kind == AiIntakeKind.Construction, cancellationToken);
            if (session is null) return;
            if (session.ApprovedRfqId is Guid existingId)
            {
                result = await rfqService.GetAsync(userId, existingId, cancellationToken);
                return;
            }

            List<ClarificationTurn> turns = DeserializeTurns(session.TranscriptJson);
            if (completedInAdvancedForm)
                session.CompleteInAdvancedForm(timeProvider.GetUtcNow());
            var derivedDraft = approvedDraft with
            {
                Source = ProcurementNeedSource.Assisted,
                InputType = ProcurementInputType.ProjectDescription,
                NeedType = ProcurementNeedType.Project,
                BuyerNotes = AppendClarifications(approvedDraft.BuyerNotes, turns)
            };
            result = await rfqService.CreateAsync(userId, derivedDraft, cancellationToken);
            session.Approve(result.Id, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return result;
    }

    private async Task<ConstructionProjectInterpretationRequest> BuildContextAsync(
        AiIntakeSession session, IReadOnlyCollection<ClarificationTurn> turns, AssistedNeedDraft? previous,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var buyer = await dbContext.BuyerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == session.BuyerId, cancellationToken);
        // Construction depth uses the Construction-vertical category subset only; general categories never enter here.
        IntakeCategory[] categories = await dbContext.Categories.AsNoTracking()
            .Where(item => item.IsActive && item.VerticalId == VerticalConfiguration.ConstructionId)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .Select(item => new IntakeCategory(item.Id, item.Name, item.Slug)).ToArrayAsync(cancellationToken);
        return new(session.BuyerId, session.InitialInput, buyer?.DefaultCity ?? string.Empty, buyer?.DefaultCounty ?? string.Empty,
            buyer?.MarketRadiusKm ?? 30, categories, turns, previous, now);
    }

    private async Task<ConstructionProjectInterpretation> InterpretAsync(
        ConstructionProjectInterpretationRequest context, CancellationToken cancellationToken)
    {
        EnsureAiAvailable();
        try
        {
            return await interpreter.InterpretAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AiIntakeUnavailableException)
        {
            LogProviderUnavailable(logger, exception);
            throw new AiIntakeUnavailableException(
                "Poți continua același proiect în formularul complet; informațiile deja salvate vor fi păstrate.", exception);
        }
    }

    private static void Apply(AiIntakeSession session, IReadOnlyCollection<ClarificationTurn> turns,
        ConstructionProjectInterpretation outcome, DateTimeOffset now) => session.ApplyExtraction(
            JsonSerializer.Serialize(turns, JsonOptions), JsonSerializer.Serialize(outcome, JsonOptions),
            outcome.Provider, outcome.Model, outcome.PromptVersion, outcome.InputTokens, outcome.OutputTokens,
            outcome.Clarification is not null, now);

    private static ConstructionProjectInterpretation DeserializeOutcome(string json) =>
        JsonSerializer.Deserialize<ConstructionProjectInterpretation>(json, JsonOptions)
        ?? throw new InvalidOperationException("Stored construction-intake extraction is invalid.");

    private static List<ClarificationTurn> DeserializeTurns(string json) =>
        JsonSerializer.Deserialize<List<ClarificationTurn>>(json, JsonOptions) ?? [];

    private static ConstructionIntakeResult ToResult(AiIntakeSession session, ConstructionProjectInterpretation outcome) => new(
        session.Id, session.InitialInput, session.Status, session.Provider, session.Model, session.PromptVersion,
        outcome.ProjectSummary, outcome.Draft, outcome.Assumptions, outcome.Uncertainties, outcome.Clarification,
        DeserializeTurns(session.TranscriptJson).Select(turn =>
            new AssistedIntakeAnswer(turn.Field, turn.Question, turn.Answer)).ToArray(),
        session.InputTokens, session.OutputTokens, session.ApprovedRfqId);

    private static string? AppendClarifications(string? buyerNotes, List<ClarificationTurn> turns)
    {
        if (turns.Count == 0) return buyerNotes;
        const string heading = "Clarificări din proiectul de construcții:";
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
        if (!settings.Enabled) throw new AiIntakeRuleException("Modul Proiect de construcții este momentan indisponibil.");
    }

    private void EnsureAiAvailable()
    {
        if (!interpreter.IsAvailable)
            throw new AiIntakeUnavailableException("Continuă cu formularul complet pentru a crea proiectul.");
    }

    [LoggerMessage(EventId = 4111, Level = LogLevel.Warning,
        Message = "Construction intake provider failed; the client will continue in the advanced form.")]
    private static partial void LogProviderUnavailable(ILogger logger, Exception exception);
}
