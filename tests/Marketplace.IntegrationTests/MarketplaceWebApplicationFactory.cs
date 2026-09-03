using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Bidding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Shared application host for integration tests. It isolates profile writes from external geocoding while
/// retaining deterministic persisted coordinates, and it makes the LLM hermetic by DEFAULT by replacing
/// <see cref="ILlmProvider"/> with <see cref="StaticMockLlmProvider"/> at the DI-registration level — the CI/CD
/// and local test hosts therefore NEVER depend on a real Anthropic key (none exists on CI; locally the key
/// lives only in user-secrets for manual runs). The <c>AI:UseMockProvider</c> config flag is NOT sufficient on
/// its own under WebApplicationFactory (the DI decision reads configuration at registration time, before a
/// test's <c>ConfigureAppConfiguration</c> override is applied — proven by auto-bid 503s when the flag alone
/// was set), so the provider swap is done here as a service override. A test that needs a specific LLM
/// contract replaces <see cref="ILlmProvider"/> explicitly on its own host (RemoveAll + Add), exactly as the
/// DI comment in DependencyInjection documents: „standard tests never call this provider (they use
/// StaticMockLlmProvider)". TODO(post-MVP): when keys are separated per environment, keep this hermetic
/// default and opt in to live-LLM contract tests explicitly.
/// </summary>
public sealed class MarketplaceWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:UseMockProvider"] = "true"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILocationGeocoder>();
            services.AddScoped<ILocationGeocoder, DeterministicLocationGeocoder>();
            // Hermetic LLM default: never let a test host call the real Anthropic provider. Tests that need a
            // particular LLM contract remove this and add their own provider.
            services.RemoveAll<ILlmProvider>();
            services.AddSingleton<ILlmProvider>(new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal));
        });
    }
}

internal sealed class DeterministicLocationGeocoder : ILocationGeocoder
{
    public Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken) =>
        Task.FromResult(new GeographicLocation(input.Address, input.Locality, input.County, input.Country, 47.7m, 25.8m));
}
