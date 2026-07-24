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
        await ValidateReferences(buyerId, draft.PrimaryCategoryId, cancellationToken);
        Rfq rfq = Rfq.Create(Guid.NewGuid(), buyerId, draft, timeProvider.GetUtcNow());
        dbContext.Rfqs.Add(rfq);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> UpdateAsync(Guid buyerId, Guid rfqId, RfqDraft draft, CancellationToken cancellationToken)
    {
        await ValidateReferences(buyerId, draft.PrimaryCategoryId, cancellationToken);
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Update(draft, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> PublishAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Publish(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> CancelAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        rfq.Cancel(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
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
            .Where(rfq => rfq.Status == RfqStatus.Published
                && rfq.QuotationDeadline > now
                && supplierCategoryIds.Contains(rfq.PrimaryCategoryId)
                && (nationwide || deliveryCounties.Contains(rfq.County)));
        if (categoryId.HasValue)
            query = query.Where(rfq => rfq.PrimaryCategoryId == categoryId.Value);

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
            buyers.GetValueOrDefault(rfq.BuyerId, "Cumpărător verificat"))).ToArray();
    }

    public async Task<OpenRfq?> GetOpenAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken) =>
        (await GetOpenAsync(supplierId, null, null, cancellationToken)).SingleOrDefault(item => item.Rfq.Id == rfqId);

    private Task<Rfq?> Find(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.Include(rfq => rfq.Items).SingleOrDefaultAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken);

    private async Task ValidateReferences(Guid buyerId, Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await dbContext.BuyerProfiles.AnyAsync(profile => profile.Id == buyerId, cancellationToken))
            throw new RfqRuleException("Completați și salvați mai întâi profilul de cumpărător.");
        bool validCategory = await dbContext.Categories.AnyAsync(category => category.Id == categoryId && category.IsActive, cancellationToken);
        if (!validCategory) throw new RfqRuleException("Categoria principală selectată nu este disponibilă.");
    }
}
