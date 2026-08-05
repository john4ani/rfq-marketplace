using Marketplace.Infrastructure.Identity;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Marketplace.Domain.LiveMarket;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Notifications;
using Marketplace.Domain.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();
    public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierDeliverySettings> SupplierDeliverySettings => Set<SupplierDeliverySettings>();
    public DbSet<SupplierDeliveryArea> SupplierDeliveryAreas => Set<SupplierDeliveryArea>();
    public DbSet<UserPresence> UserPresences => Set<UserPresence>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqItem> RfqItems => Set<RfqItem>();
    public DbSet<ProcurementNeedAuditEvent> ProcurementNeedAuditEvents => Set<ProcurementNeedAuditEvent>();
    public DbSet<AiIntakeSession> AiIntakeSessions => Set<AiIntakeSession>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferItem> OfferItems => Set<OfferItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);
    }
}
