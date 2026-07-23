using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Infrastructure.Identity;

public static class IdentitySeeder
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);

    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        await SeedLock.WaitAsync();
        try
        {
            using IServiceScope scope = services.CreateScope();
            RoleManager<IdentityRole<Guid>> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            foreach (string role in MarketplaceRoles.All)
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
        finally
        {
            SeedLock.Release();
        }
    }
}
