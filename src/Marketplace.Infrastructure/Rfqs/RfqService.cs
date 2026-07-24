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
