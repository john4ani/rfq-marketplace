using Marketplace.Infrastructure.Identity;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Marketplace.Domain.LiveMarket;
using Marketplace.Domain.Rfqs;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);
    }
}
