using Marketplace.Application.Rfqs;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Rfqs;

public sealed class RfqService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : IRfqService
{
    public async Task<IReadOnlyCollection<Rfq>> GetMineAsync(Guid buyerId, CancellationToken cancellationToken) =>
        await dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.Items).Where(rfq => rfq.BuyerId == buyerId)
            .OrderByDescending(rfq => rfq.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Rfq?> GetAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.Items)
            .SingleOrDefaultAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken);

    public async Task<Rfq> CreateAsync(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken)
    {
        await ValidateReferences(buyerId, draft, cancellationToken);
        Rfq rfq = Rfq.Create(Guid.NewGuid(), buyerId, draft, timeProvider.GetUtcNow());
        dbContext.Rfqs.Add(rfq);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> UpdateAsync(Guid buyerId, Guid rfqId, RfqDraft draft, CancellationToken cancellationToken)
    {
        await ValidateReferences(buyerId, draft, cancellationToken);
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Update(draft, timeProvider.GetUtcNow());
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> PublishAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Publish(timeProvider.GetUtcNow());
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> CancelAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Cancel(timeProvider.GetUtcNow());
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> ExtendQuotationDeadlineAsync(
        Guid buyerId, Guid rfqId, DateTimeOffset newDeadline, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.ExtendQuotationDeadline(newDeadline, timeProvider.GetUtcNow());
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<IReadOnlyCollection<OpenRfq>> GetOpenAsync(
        Guid supplierId, string? search, Guid? categoryId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] supplierCategoryIds = await dbContext.SupplierCategories.AsNoTracking()
            .Where(selection => selection.SupplierId == supplierId)
            .Select(selection => selection.CategoryId)
            .ToArrayAsync(cancellationToken);
        if (supplierCategoryIds.Length == 0) return [];

        string? supplierCounty = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.Id == supplierId && profile.IsActive)
            .Select(profile => profile.County)
            .SingleOrDefaultAsync(cancellationToken);
        if (supplierCounty is null) return [];

        bool nationwide = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Where(settings => settings.SupplierId == supplierId)
            .Select(settings => settings.NationwideDelivery)
            .SingleOrDefaultAsync(cancellationToken);
        string[] configuredCounties = await dbContext.SupplierDeliveryAreas.AsNoTracking()
            .Where(area => area.SupplierId == supplierId)
            .Select(area => area.County)
            .ToArrayAsync(cancellationToken);
        string[] deliveryCounties = configuredCounties.Length > 0 ? configuredCounties : [supplierCounty];

        IQueryable<Rfq> query = dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.Items)
            .Where(rfq => (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline > now
                && (supplierCategoryIds.Contains(rfq.PrimaryCategoryId)
                    || rfq.Items.Any(item => item.CategoryId.HasValue && supplierCategoryIds.Contains(item.CategoryId.Value)))
                && (nationwide || deliveryCounties.Contains(rfq.County)));
        if (categoryId.HasValue)
            query = query.Where(rfq => rfq.PrimaryCategoryId == categoryId.Value
                || rfq.Items.Any(item => item.CategoryId == categoryId.Value));

        Rfq[] rfqs = await query.OrderBy(rfq => rfq.QuotationDeadline).ThenByDescending(rfq => rfq.PublishedAt)
            .ToArrayAsync(cancellationToken);
        string cleanSearch = search?.Trim() ?? string.Empty;
        if (cleanSearch.Length > 0)
            rfqs = rfqs.Where(rfq =>
                rfq.Title.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || (rfq.ProjectName?.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || rfq.DeliveryLocality.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || rfq.Items.Any(item => item.ProductName.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        Dictionary<Guid, HashSet<Guid>> matchingItems = [];
        rfqs = await ApplyCoveragePriority(
            rfqs,
            supplierId,
            supplierCategoryIds.ToHashSet(),
            matchingItems,
            cancellationToken);

        Guid[] categoryIds = rfqs.Select(rfq => rfq.PrimaryCategoryId).Distinct().ToArray();
        Guid[] buyerIds = rfqs.Select(rfq => rfq.BuyerId).Distinct().ToArray();
        Dictionary<Guid, string> categories = await dbContext.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        Dictionary<Guid, string> buyers = await dbContext.BuyerProfiles.AsNoTracking()
            .Where(profile => buyerIds.Contains(profile.Id))
            .ToDictionaryAsync(
                profile => profile.Id,
                profile => profile.CompanyName ?? profile.DisplayName,
                cancellationToken);
        return rfqs.Select(rfq => new OpenRfq(
            rfq,
            categories.GetValueOrDefault(rfq.PrimaryCategoryId, "Categorie"),
            buyers.GetValueOrDefault(rfq.BuyerId, "Cumpărător verificat"),
            matchingItems.TryGetValue(rfq.Id, out HashSet<Guid>? ids) && ids.Count < rfq.Items.Count,
            matchingItems.GetValueOrDefault(rfq.Id))).ToArray();
    }

    public async Task<OpenRfq?> GetOpenAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken)
    {
        OpenRfq? openRequest = (await GetOpenAsync(supplierId, null, null, cancellationToken))
            .SingleOrDefault(item => item.Rfq.Id == rfqId);
        if (openRequest is not null) return openRequest;

        // A request stops being "open" when the Buyer selects an offer. The supplier who
        // participated must still be able to reopen it to see their final offer outcome.
        if (!await dbContext.Offers.AsNoTracking()
                .AnyAsync(offer => offer.RfqId == rfqId && offer.SupplierId == supplierId, cancellationToken))
            return null;

        Rfq? rfq = await dbContext.Rfqs.AsNoTracking().Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        if (rfq is null) return null;

        string categoryName = await dbContext.Categories.AsNoTracking()
            .Where(category => category.Id == rfq.PrimaryCategoryId)
            .Select(category => category.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "Categorie";
        string buyerName = await dbContext.BuyerProfiles.AsNoTracking()
            .Where(profile => profile.Id == rfq.BuyerId)
            .Select(profile => profile.CompanyName ?? profile.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "Cumpărător verificat";
        return new OpenRfq(rfq, categoryName, buyerName);
    }

    private Task<Rfq?> Find(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.Include(rfq => rfq.Items).SingleOrDefaultAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken);

    private async Task SaveRfqChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new RfqConcurrencyException(
                "Cererea a fost modificată între timp. Reîncarcă pagina înainte de a continua.",
                exception);
        }
    }

    private async Task ValidateReferences(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken)
    {
        if (!await dbContext.BuyerProfiles.AnyAsync(profile => profile.Id == buyerId, cancellationToken))
            throw new RfqRuleException("Completați și salvați mai întâi profilul de cumpărător.");
        Guid[] categoryIds = draft.Items.Where(item => item.CategoryId.HasValue).Select(item => item.CategoryId!.Value)
            .Append(draft.PrimaryCategoryId).Distinct().ToArray();
        int validCategories = await dbContext.Categories.CountAsync(
            category => categoryIds.Contains(category.Id) && category.IsActive, cancellationToken);
        if (validCategories != categoryIds.Length)
            throw new RfqRuleException("Una dintre categoriile selectate nu este disponibilă.");
    }

    private async Task<Rfq[]> ApplyCoveragePriority(
        Rfq[] rfqs,
        Guid currentSupplierId,
        HashSet<Guid> currentCategories,
        Dictionary<Guid, HashSet<Guid>> matchingItems,
        CancellationToken cancellationToken)
    {
        if (rfqs.Length == 0) return rfqs;

        var profiles = await dbContext.SupplierProfiles.AsNoTracking().Where(profile => profile.IsActive)
            .Select(profile => new { profile.Id, profile.County }).ToArrayAsync(cancellationToken);
        var selections = await dbContext.SupplierCategories.AsNoTracking()
            .Select(item => new { item.SupplierId, item.CategoryId }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<Guid>> categoriesBySupplier = selections.GroupBy(item => item.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.CategoryId).ToHashSet());
        var deliverySettings = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Select(settings => new {
                settings.SupplierId,
                settings.NationwideDelivery,
                settings.CanFulfillMultiCategoryRequests
            }).ToArrayAsync(cancellationToken);
        HashSet<Guid> nationwideSuppliers = deliverySettings.Where(settings => settings.NationwideDelivery)
            .Select(settings => settings.SupplierId).ToHashSet();
        HashSet<Guid> multiCategorySuppliers = deliverySettings.Where(settings => settings.CanFulfillMultiCategoryRequests)
            .Select(settings => settings.SupplierId).ToHashSet();
        var deliveryAreas = await dbContext.SupplierDeliveryAreas.AsNoTracking()
            .Select(area => new { area.SupplierId, area.County }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<string>> areasBySupplier = deliveryAreas.GroupBy(area => area.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(area => area.County).ToHashSet(StringComparer.OrdinalIgnoreCase));

        List<Rfq> visible = [];
        foreach (Rfq rfq in rfqs)
        {
            HashSet<Guid> requiredCategories = rfq.Items.Count > 0
                ? rfq.Items.Select(item => item.CategoryId ?? rfq.PrimaryCategoryId).ToHashSet()
                : [rfq.PrimaryCategoryId];
            Guid[] eligibleSupplierIds = profiles.Where(profile =>
                    nationwideSuppliers.Contains(profile.Id)
                    || (areasBySupplier.TryGetValue(profile.Id, out HashSet<string>? areas)
                        ? areas.Contains(rfq.County)
                        : string.Equals(profile.County, rfq.County, StringComparison.OrdinalIgnoreCase)))
                .Select(profile => profile.Id).ToArray();
            bool fullCoverageExists = eligibleSupplierIds.Any(id =>
                categoriesBySupplier.TryGetValue(id, out HashSet<Guid>? categories)
                && requiredCategories.IsSubsetOf(categories)
                && (requiredCategories.Count == 1 || multiCategorySuppliers.Contains(id)));
            ProviderCoverageMatch coverage = ProviderCoveragePolicy.Decide(
                rfq.AllowSplit,
                requiredCategories,
                currentCategories,
                multiCategorySuppliers.Contains(currentSupplierId),
                fullCoverageExists);
            if (coverage == ProviderCoverageMatch.None) continue;

            HashSet<Guid> itemIds = coverage == ProviderCoverageMatch.Full
                ? rfq.Items.Select(item => item.Id).ToHashSet()
                : rfq.Items.Where(item => currentCategories.Contains(item.CategoryId ?? rfq.PrimaryCategoryId))
                    .Select(item => item.Id).ToHashSet();
            matchingItems[rfq.Id] = itemIds;
            visible.Add(rfq);
        }
        return visible.ToArray();
    }
}
