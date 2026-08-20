using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Shared application host for integration tests. It isolates profile writes from
/// external geocoding while retaining deterministic persisted coordinates.
/// </summary>
public sealed class MarketplaceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILocationGeocoder>();
            services.AddScoped<ILocationGeocoder, DeterministicLocationGeocoder>();
        });
    }
}

internal sealed class DeterministicLocationGeocoder : ILocationGeocoder
{
    public Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken) =>
        Task.FromResult(new GeographicLocation(input.Address, input.Locality, input.County, input.Country, 47.7m, 25.8m));
}
