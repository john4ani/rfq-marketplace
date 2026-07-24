using System.Globalization;
using System.Text;
using Marketplace.Application.Suppliers;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Suppliers;

public sealed class SupplierSettingsService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : ISupplierSettingsService
{
    public async Task<IReadOnlyCollection<Category>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder).ThenBy(category => category.Name).ToListAsync(cancellationToken);

    public async Task<CategoryCreationResult> AddCategoryAsync(string name, bool confirmSimilar, CancellationToken cancellationToken)
    {
        string cleanName = name.Trim();
        string slug = Slug(cleanName);
        Category? existing = await dbContext.Categories.SingleOrDefaultAsync(category => category.Slug == slug, cancellationToken);
        if (existing is not null) return new(existing, true, []);

        Category[] similarCategories = (await dbContext.Categories.AsNoTracking()
                .Where(category => category.IsActive && category.ParentId != null)
                .ToArrayAsync(cancellationToken))
            .Where(category => AreSimilar(slug, category.Slug))
            .OrderBy(category => category.Name)
            .ToArray();
        if (similarCategories.Length > 0 && !confirmSimilar)
            return new(null, false, similarCategories);
        Guid rootId = await dbContext.Categories.Where(category => category.ParentId == null)
            .Select(category => category.Id).SingleAsync(cancellationToken);
        int lastOrder = await dbContext.Categories.Where(category => category.ParentId == rootId)
            .MaxAsync(category => (int?)category.SortOrder, cancellationToken) ?? 0;
        Category category = Category.Create(Guid.NewGuid(), rootId, cleanName, slug, lastOrder + 10);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(category, false, similarCategories);
    }

    public async Task<SupplierCategorySelection?> GetCategoriesAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!await SupplierExists(supplierId, cancellationToken)) return null;
        Guid[] ids = await dbContext.SupplierCategories.AsNoTracking().Where(item => item.SupplierId == supplierId)
            .Select(item => item.CategoryId).ToArrayAsync(cancellationToken);
        return new(ids);
    }

    public async Task<SupplierCategorySelection?> SaveCategoriesAsync(Guid supplierId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        if (!await SupplierExists(supplierId, cancellationToken)) return null;
        SupplierCategory[] existing = await dbContext.SupplierCategories.Where(item => item.SupplierId == supplierId).ToArrayAsync(cancellationToken);
        Guid[] selected = categoryIds.Distinct().ToArray();
        HashSet<Guid> selectedIds = selected.ToHashSet();
        HashSet<Guid> existingIds = existing.Select(item => item.CategoryId).ToHashSet();
        dbContext.SupplierCategories.RemoveRange(existing.Where(item => !selectedIds.Contains(item.CategoryId)));
        dbContext.SupplierCategories.AddRange(selected.Where(categoryId => !existingIds.Contains(categoryId)).Select(categoryId => SupplierCategory.Create(supplierId, categoryId)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(selected);
    }

    public async Task<SupplierDeliverySelection?> GetDeliverySettingsAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        if (!await SupplierExists(supplierId, cancellationToken)) return null;
        SupplierDeliverySettings? settings = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.SupplierId == supplierId, cancellationToken);
        string[] counties = await dbContext.SupplierDeliveryAreas.AsNoTracking().Where(area => area.SupplierId == supplierId)
            .OrderBy(area => area.County).Select(area => area.County).ToArrayAsync(cancellationToken);
        return settings is null ? new(50, 100, null, false, counties) : ToSelection(settings, counties);
    }

    public async Task<SupplierDeliverySelection?> SaveDeliverySettingsAsync(Guid supplierId, SupplierDeliveryInput input, CancellationToken cancellationToken)
    {
        if (!await SupplierExists(supplierId, cancellationToken)) return null;
        SupplierDeliverySettings? settings = await dbContext.SupplierDeliverySettings.SingleOrDefaultAsync(item => item.SupplierId == supplierId, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (settings is null)
        {
            settings = SupplierDeliverySettings.Create(supplierId, input.NormalDeliveryRadiusKm, input.MaximumDeliveryRadiusKm, input.MinimumOrderValue, input.NationwideDelivery, now);
            dbContext.SupplierDeliverySettings.Add(settings);
        }
        else settings.Update(input.NormalDeliveryRadiusKm, input.MaximumDeliveryRadiusKm, input.MinimumOrderValue, input.NationwideDelivery, now);

        SupplierDeliveryArea[] currentAreas = await dbContext.SupplierDeliveryAreas.Where(area => area.SupplierId == supplierId).ToArrayAsync(cancellationToken);
        string[] counties = input.NationwideDelivery ? [] : input.Counties.Distinct(StringComparer.Ordinal).Order().ToArray();
        HashSet<string> selectedCounties = counties.ToHashSet(StringComparer.Ordinal);
        HashSet<string> existingCounties = currentAreas.Select(area => area.County).ToHashSet(StringComparer.Ordinal);
        dbContext.SupplierDeliveryAreas.RemoveRange(currentAreas.Where(area => !selectedCounties.Contains(area.County)));
        dbContext.SupplierDeliveryAreas.AddRange(counties.Where(county => !existingCounties.Contains(county)).Select(county => SupplierDeliveryArea.Create(supplierId, county)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSelection(settings, counties);
    }

    private Task<bool> SupplierExists(Guid supplierId, CancellationToken cancellationToken) =>
        dbContext.SupplierProfiles.AnyAsync(profile => profile.Id == supplierId, cancellationToken);

    private static SupplierDeliverySelection ToSelection(SupplierDeliverySettings settings, IReadOnlyCollection<string> counties) =>
        new(settings.NormalDeliveryRadiusKm, settings.MaximumDeliveryRadiusKm, settings.MinimumOrderValue, settings.NationwideDelivery, counties);

    private static string Slug(string value)
    {
        var result = new StringBuilder();
        bool separator = false;
        foreach (char character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character))
            {
                if (separator && result.Length > 0) result.Append('-');
                result.Append(char.ToLowerInvariant(character));
                separator = false;
            }
            else separator = true;
        }
        return result.ToString();
    }

    private static bool AreSimilar(string left, string right)
    {
        if (left.Length < 3 || right.Length < 3) return false;
        if ((left.Contains(right, StringComparison.Ordinal) || right.Contains(left, StringComparison.Ordinal))
            && Math.Min(left.Length, right.Length) >= 4)
            return true;

        int allowedDistance = Math.Max(1, Math.Min(3, Math.Min(left.Length, right.Length) / 4));
        if (Math.Abs(left.Length - right.Length) > allowedDistance) return false;
        return EditDistance(left, right) <= allowedDistance;
    }

    private static int EditDistance(string left, string right)
    {
        int[] previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[] current = new int[right.Length + 1];
        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
