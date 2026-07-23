using Marketplace.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);
    }
}
