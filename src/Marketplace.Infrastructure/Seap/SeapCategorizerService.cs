using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Suppliers;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Maps a SEAP CPV label (e.g. "Montare de garduri") to a CompaSpot category. The first tender with a
/// standardized CPV code pays one LLM call; every later tender resolves instantly from the persistent
/// <see cref="CpvCategoryMapping"/> table. Categories the LLM returns that do not exist yet are
/// auto-created under the "Achiziții generale" root. All failures degrade to null so the tender still
/// imports — categorization is enrichment, never a gate.
/// </summary>
public sealed partial class SeapCategorizerService(
    MarketplaceDbContext dbContext,
    ILlmProvider llmProvider,
    ILogger<SeapCategorizerService> logger) : ISeapCategorizer
{
    private const string SystemPrompt =
        "Ești un expert în taxonomia CPV a achizițiilor publice europene și în categoriile de " +
        "achiziții din CompaSpot (o piață de achiziții B2B). Clasifici un cod CPV într-o singură " +
        "categorie de achiziții. Răspunzi DOAR cu JSON valid de forma {\"category\": \"Numele categoriei\"}.";

    public async Task<SeapCategorizationResult?> CategorizeAsync(
        string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cpvCode)) return null;

        // 1) Persistent cache: the same standardized CPV code always maps to the same category, so the
        //    LLM is called once per unique code, not once per tender.
        CpvCategoryMapping? cached = await dbContext.CpvCategoryMappings.AsNoTracking()
            .Include(mapping => mapping.Category)
            .SingleOrDefaultAsync(mapping => mapping.CpvCode == cpvCode, cancellationToken);
        if (cached is not null)
            return new SeapCategorizationResult(cached.CategoryId, cached.Category?.Name ?? string.Empty, false, true);

        try
        {
            // 2) One LLM call per unique CPV code.
            string userPrompt =
                $"Cod CPV: {cpvCode}\n" +
                (string.IsNullOrWhiteSpace(cpvName) ? string.Empty : $"Nume CPV: {cpvName}\n") +
                $"Titlu anunț: {title}\n\n" +
                "Categoria CompaSpot cea mai potrivită este:";
            string llmJson = await llmProvider.CompleteAsync(SystemPrompt, userPrompt, cancellationToken);
            string? categoryName = ParseCategoryName(llmJson);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                LogCategorizationUnparseable(logger, cpvCode);
                return null;
            }

            // 3) Find or auto-create. The new Category and the cache row are staged on the same context
            //    and persist with the importing tender in the caller's single SaveChanges.
            Category category = await FindOrCreateCategoryAsync(categoryName, cancellationToken);
            bool isNew = category.Id == Guid.Empty || dbContext.Entry(category).State == EntityState.Added;
            dbContext.CpvCategoryMappings.Add(CpvCategoryMapping.Create(cpvCode, category.Id, DateTimeOffset.UtcNow));
            LogCategorized(logger, cpvCode, category.Name, isNew ? "auto-created" : "existing");
            return new SeapCategorizationResult(category.Id, category.Name, isNew, false);
        }
        catch (Exception exception)
        {
            LogCategorizationFailed(logger, cpvCode, exception);
            return null;
        }
    }

    private async Task<Category> FindOrCreateCategoryAsync(string categoryName, CancellationToken cancellationToken)
    {
        string key = Slugify(categoryName);
        List<Category> existing = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToListAsync(cancellationToken);
        Category? match = existing.FirstOrDefault(category =>
            Slugify(category.Name) == key || category.Slug == key);
        if (match is not null) return match;

        Category created = Category.Create(
            Guid.NewGuid(),
            VerticalConfiguration.GeneralProcurementCompatibilityId,
            CategoryConfiguration.GeneralProcurementRootId,
            categoryName.Trim(),
            Slugify(categoryName),
            2000);
        dbContext.Categories.Add(created);
        return created;
    }

    private static string? ParseCategoryName(string llmJson)
    {
        try
        {
            int start = llmJson.IndexOf('{');
            int end = llmJson.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            using JsonDocument document = JsonDocument.Parse(llmJson[start..(end + 1)]);
            if (document.RootElement.TryGetProperty("category", out JsonElement category) &&
                category.ValueKind == JsonValueKind.String)
                return category.GetString()?.Trim();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Slugify(string value)
    {
        string lower = value.ToLowerInvariant();
        string decomposed = new(lower.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        string slug = NonAlphanumericRegex().Replace(decomposed, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "categorie" : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [LoggerMessage(EventId = 5111, Level = LogLevel.Information, Message = "SEAP CPV {CpvCode} categorized as {Category} ({MatchKind}).")]
    private static partial void LogCategorized(ILogger logger, string cpvCode, string category, string matchKind);
    [LoggerMessage(EventId = 5112, Level = LogLevel.Warning, Message = "SEAP CPV {CpvCode} LLM output could not be parsed into a category; tender stays uncategorized.")]
    private static partial void LogCategorizationUnparseable(ILogger logger, string cpvCode);
    [LoggerMessage(EventId = 5113, Level = LogLevel.Warning, Message = "SEAP CPV {CpvCode} categorization failed; tender stays uncategorized.")]
    private static partial void LogCategorizationFailed(ILogger logger, string cpvCode, Exception exception);
}
