using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Design;

namespace Marketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__MarketplaceDatabase")
            ?? "Host=localhost;Port=5432;Database=marketplace;Username=marketplace;Password=marketplace-local-only";

        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new MarketplaceDbContext(options);
    }
}
