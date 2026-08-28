using Marketplace.Application.AiIntake;
using Marketplace.Application.Seap;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.AiIntake;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Seap;

// SEAP Tender Match orchestration: translates a free-text description through the dedicated interpreter and
// persists the confirmed CPV-prefix + county filters on the represented company (SupplierProfile), where the
// Anunțuri SEAP list reads them as deterministic defaults. It does not create a Procurement Need, so it does
// not reuse AiIntakeSession (which is tied to Rfq approval) — the chat is stateless and only the confirmed
// filters are persisted.
internal sealed partial class SeapTenderMatchService(
    MarketplaceDbContext dbContext,
    ISeapTenderMatchInterpreter interpreter,
    TimeProvider timeProvider,
    IOptions<AiIntakeOptions> options,
    ILogger<SeapTenderMatchService> logger) : ISeapTenderMatchService
{
    private readonly AiIntakeOptions settings = options.Value;

    public AssistedIntakeAvailability Availability => new(settings.Enabled, interpreter.IsAvailable);

    public async Task<SeapTenderMatchResult> InterpretAsync(
        string input, IReadOnlyCollection<SeapClarificationTurn> turns, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        EnsureAiAvailable();
        SeapTenderMatchRequest request = new(CleanInput(input), turns ?? []);
        try
        {
            return await interpreter.InterpretAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not AiIntakeUnavailableException)
        {
            LogProviderUnavailable(logger, exception);
            throw new AiIntakeUnavailableException(
                "Poți configura filtrele manual în Anunțuri SEAP; informațiile deja extrase sunt păstrate.", exception);
        }
    }

    public async Task<SeapTenderMatchProfile> SaveFiltersAsync(
        Guid companyId, IReadOnlyCollection<string> cpvPrefixes, IReadOnlyCollection<string> counties,
        CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await dbContext.SupplierProfiles
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken)
            ?? throw new AiIntakeRuleException("Firma nu a fost găsită. Configurează mai întâi firma din secțiunea „Firma mea”.");
        string[] normalizedPrefixes = (cpvPrefixes ?? [])
            .Select(CpvPrefix.TryNormalize).Where(prefix => prefix is not null).Select(prefix => prefix!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] normalizedCounties = (counties ?? [])
            .Select(RomanianCountyCodes.TryNormalize).Where(county => county is not null).Select(county => county!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        profile.SetSeapFilters(normalizedPrefixes, normalizedCounties, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(profile.Id, profile.SeapCpvPrefixes, profile.SeapCounties);
    }

    private static string CleanInput(string? input)
    {
        string clean = input?.Trim() ?? string.Empty;
        if (clean.Length is < 10 or > 4000)
            throw new AiIntakeRuleException("Descrierea trebuie să aibă între 10 și 4.000 de caractere.");
        return clean;
    }

    private void EnsureEnabled()
    {
        if (!settings.Enabled) throw new AiIntakeRuleException("Potrivirea de anunțuri SEAP este momentan indisponibilă.");
    }

    private void EnsureAiAvailable()
    {
        if (!interpreter.IsAvailable) throw new AiIntakeUnavailableException("Configurează filtrele manual în Anunțuri SEAP.");
    }

    [LoggerMessage(EventId = 4121, Level = LogLevel.Warning,
        Message = "SEAP tender match provider failed; the client can edit filters manually.")]
    private static partial void LogProviderUnavailable(ILogger logger, Exception exception);
}
