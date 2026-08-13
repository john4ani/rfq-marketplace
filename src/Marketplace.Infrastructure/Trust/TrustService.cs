using Marketplace.Application.Trust;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Trust;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Trust;

public sealed class TrustService(
    MarketplaceDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ITrustService
{
    public async Task<AccountTrustSummary?> GetForAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        return user is null ? null : await BuildAsync(user, cancellationToken);
    }

    public async Task<TrustFacts?> GetFactsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (user is null) return null;
        AccountTrustRecord? trust = await dbContext.AccountTrustRecords.AsNoTracking().SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        int published = await dbContext.Rfqs.CountAsync(item => item.BuyerId == accountId && item.PublishedAt != null, cancellationToken);
        int completed = await dbContext.Rfqs.CountAsync(item => item.BuyerId == accountId && item.Status == RfqStatus.Closed, cancellationToken);
        return new(user.EmailConfirmed, trust?.PhoneVerifiedAt is not null, false, user.CreatedAt, published, completed, 0, 0);
    }

    public async Task<TrustFacts?> GetCompanyFactsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        SupplierProfile? company = await dbContext.SupplierProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null) return null;
        AccountTrustRecord? legacyTrust = await dbContext.AccountTrustRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.AccountId == companyId, cancellationToken);
        int submitted = await dbContext.Offers.CountAsync(item => item.SupplierId == companyId
            && item.Status != OfferStatus.Draft && item.Status != OfferStatus.Withdrawn, cancellationToken);
        int collaborations = await dbContext.Offers.CountAsync(item => item.SupplierId == companyId
            && item.Status == OfferStatus.Selected, cancellationToken);
        return new(false, false, legacyTrust?.CompanyVerifiedAt is not null, company.CreatedAt, 0, 0, submitted, collaborations);
    }

    public async Task<IReadOnlyCollection<AccountTrustSummary>> SearchAsync(string? search, CancellationToken cancellationToken)
    {
        string value = search?.Trim() ?? string.Empty;
        IQueryable<ApplicationUser> query = dbContext.Users.AsNoTracking().OrderBy(item => item.Email);
        if (!string.IsNullOrWhiteSpace(value))
            query = query.Where(item => item.Email!.Contains(value) || item.UserName!.Contains(value));
        ApplicationUser[] users = await query.Take(50).ToArrayAsync(cancellationToken);
        var results = new List<AccountTrustSummary>(users.Length);
        foreach (ApplicationUser user in users) results.Add(await BuildAsync(user, cancellationToken));
        return results;
    }

    public async Task<AccountTrustSummary?> ReviewAsync(Guid administratorId, Guid accountId, TrustReviewInput input, CancellationToken cancellationToken)
    {
        ApplicationUser? account = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (account is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        AccountTrustRecord? record = await dbContext.AccountTrustRecords.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (record is null) { record = AccountTrustRecord.Create(accountId, now); dbContext.AccountTrustRecords.Add(record); }
        record.Review(input.PhoneVerified, input.CompanyVerified, input.Note, administratorId, now);
        dbContext.AccountAuditEvents.Add(AccountAuditEvent.Create(accountId, administratorId, AccountAuditEventType.VerificationReviewed,
            $"Telefon: {(input.PhoneVerified ? "confirmat" : "neconfirmat")}; companie: {(input.CompanyVerified ? "confirmată" : "neconfirmată")}", now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildAsync(account, cancellationToken);
    }

    private async Task<AccountTrustSummary> BuildAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        AccountTrustRecord? trust = await dbContext.AccountTrustRecords.AsNoTracking().SingleOrDefaultAsync(item => item.AccountId == user.Id, cancellationToken);
        BuyerProfile? buyer = await dbContext.BuyerProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == user.Id, cancellationToken);
        SupplierProfile? supplier = await dbContext.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == user.Id, cancellationToken);
        IReadOnlyCollection<string> roles = (await userManager.GetRolesAsync(user)).ToArray();
        int completed = 0, total = 0;
        if (roles.Contains("Buyer")) { total += 5; if (buyer is not null) { if (!string.IsNullOrWhiteSpace(buyer.DisplayName)) completed++; if (!string.IsNullOrWhiteSpace(buyer.PhoneNumber)) completed++; if (!string.IsNullOrWhiteSpace(buyer.DefaultCity)) completed++; if (!string.IsNullOrWhiteSpace(buyer.DefaultCounty)) completed++; if (buyer.BuyerType != Marketplace.Domain.Profiles.BuyerType.Company || !string.IsNullOrWhiteSpace(buyer.CompanyName) && !string.IsNullOrWhiteSpace(buyer.TaxIdentifier)) completed++; } }
        if (roles.Contains("Supplier")) { total += 6; if (supplier is not null) { if (!string.IsNullOrWhiteSpace(supplier.TradingName)) completed++; if (!string.IsNullOrWhiteSpace(supplier.CompanyRegistrationNumber)) completed++; if (!string.IsNullOrWhiteSpace(supplier.ContactPerson)) completed++; if (!string.IsNullOrWhiteSpace(supplier.ContactEmail)) completed++; if (!string.IsNullOrWhiteSpace(supplier.PhoneNumber)) completed++; if (!string.IsNullOrWhiteSpace(supplier.Address) && !string.IsNullOrWhiteSpace(supplier.City) && !string.IsNullOrWhiteSpace(supplier.County)) completed++; } }
        int published = await dbContext.Rfqs.CountAsync(item => item.BuyerId == user.Id && item.PublishedAt != null, cancellationToken);
        Guid[] representedCompanyIds = await dbContext.CompanyMemberships.AsNoTracking()
            .Where(item => item.UserId == user.Id && item.RevokedAt == null).Select(item => item.CompanyId).ToArrayAsync(cancellationToken);
        int submitted = await dbContext.Offers.CountAsync(item => representedCompanyIds.Contains(item.SupplierId)
            && item.Status != OfferStatus.Draft && item.Status != OfferStatus.Withdrawn, cancellationToken);
        int selected = await dbContext.Offers.CountAsync(item => representedCompanyIds.Contains(item.SupplierId)
            && item.Status == OfferStatus.Selected, cancellationToken);
        TrustAuditEventView[] activity = await dbContext.AccountAuditEvents.AsNoTracking().Where(item => item.AccountId == user.Id).OrderByDescending(item => item.OccurredAt).Take(10).Select(item => new TrustAuditEventView(item.Type.ToString(), item.Details, item.OccurredAt)).ToArrayAsync(cancellationToken);
        return new(user.Id, user.Email ?? string.Empty, roles, user.EmailConfirmed, trust?.PhoneVerifiedAt is not null, trust?.PhoneVerifiedAt, trust?.CompanyVerifiedAt is not null, trust?.CompanyVerifiedAt, total == 0 ? 0 : completed * 100 / total, published, submitted, selected, activity);
    }
}
