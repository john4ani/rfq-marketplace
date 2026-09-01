using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

// Fair-pricing (Task 3): the pricing endpoint previews the dynamic EUR tier from the tender's estimated
// value, and every account starts with one free generation (launch offer).
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapFairPricingEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public SeapFairPricingEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task PricingPreviewUsesDynamicEurTierFromEstimatedValue()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"pricing-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // 125.000 RON ÷ 5 (RonToEurRate default) = 25.000 EUR → the 20k–100k tier → 75 €.
        Guid tenderId = await SeedTenderAsync(estimatedValueRon: 125_000m);

        PricingPreview preview = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/pricing"))
            .Content.ReadFromJsonAsync<PricingPreview>())!;

        Assert.Equal(25_000m, preview.EstimatedValueEur);
        Assert.Equal(75, preview.PriceEur);
        Assert.Equal(1, preview.FreeGenerationsRemaining);
    }

    [Fact]
    public async Task PricingPreviewAppliesHighTierForLargeTenders()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"pricing-high-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // 500.000.000 RON ÷ 5 = 100.000.000 EUR → ≥50M tier → 20.000 €.
        Guid tenderId = await SeedTenderAsync(estimatedValueRon: 500_000_000m);

        PricingPreview preview = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/pricing"))
            .Content.ReadFromJsonAsync<PricingPreview>())!;

        Assert.Equal(20_000, preview.PriceEur);
    }

    [Fact]
    public async Task PricingPreviewRequiresAuthentication()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        HttpResponseMessage response = await client.GetAsync($"/api/v1/seap/tenders/{Guid.NewGuid()}/pricing");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> SeedTenderAsync(decimal estimatedValueRon)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"EXT-{Guid.NewGuid():N}", "Lucrări de construcții", "Caiet de sarcini pentru lucrări de construcții",
            ["45200000-1"], ["Montare de garduri"], estimatedValueRon, "RON",
            "Primăria Test", "Iași", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30),
            DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record PricingPreview(decimal EstimatedValueEur, int PriceEur, int FreeGenerationsRemaining, bool ShadowMode);
}
