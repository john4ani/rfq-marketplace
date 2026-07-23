using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("MarketplaceDatabase")
            ?? throw new InvalidOperationException("Connection string 'MarketplaceDatabase' is required.");

        services.AddDbContext<MarketplaceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName)));

        return services;
    }
}
