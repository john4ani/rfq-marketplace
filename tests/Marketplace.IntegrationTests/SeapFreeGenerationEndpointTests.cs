using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.Companies;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

// Free auto-bid generation entitlement: consumed consistently in Shadow Mode (so the Staging/Prod UI
// stops showing "Prima generare este gratuită" after the offer was used) and re-grantable by an
// Administrator from the registered-company details.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapFreeGenerationEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory _factory;

    public SeapFreeGenerationEndpointTests(MarketplaceWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ShadowModeAutoBidConsumesTheFreeLaunchOffer()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:AutoBiddingEnabled"] = "true",
                ["AutoBidding:IsShadowMode"] = "true",
                // Development now defaults to the real Claude provider; the test must stay deterministic
                // and cost-free, so force the preset-JSON mock for the generation step.
                ["AI:UseMockProvider"] = "true"
            })));
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"shadow-free-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        Guid tenderId = await SeedTenderAsync(factory);

        PricingPreview before = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/pricing"))
            .Content.ReadFromJsonAsync<PricingPreview>())!;
        Assert.Equal(1, before.FreeGenerationsRemaining);

        AutoBidResponse bid = (await (await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid", new { }))
            .Content.ReadFromJsonAsync<AutoBidResponse>())!;
        Assert.True(bid.Ready);
        Assert.True(bid.ShadowMode);
        Assert.True(bid.FreeGeneration);

        PricingPreview after = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/pricing"))
            .Content.ReadFromJsonAsync<PricingPreview>())!;
        Assert.Equal(0, after.FreeGenerationsRemaining);
    }

    [Fact]
    public async Task AdministratorCanGrantAnExtraFreeGenerationToARegisteredCompany()
    {
        using HttpClient client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"grant-free-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);

        TokenResponse token = (await (await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid companyId = UserId(token.AccessToken);
        using HttpClient owner = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PutAsJsonAsync($"/api/v1/suppliers/{companyId}", SupplierRequest(email))).StatusCode);

        await GrantAdministratorRole(email);
        TokenResponse adminToken = (await (await _factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        using HttpClient admin = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.AccessToken);

        CompanyRegistryEntry granted = (await (await admin
            .PostAsJsonAsync($"/api/v1/admin/companies/registered/{companyId}/free-generation", new { }))
            .Content.ReadFromJsonAsync<CompanyRegistryEntry>())!;

        Assert.Equal(companyId, granted.Id);
        Assert.Equal(2, granted.FreeGenerationsRemaining); // launch offer (1) + the granted one (1)
    }

    private static async Task<Guid> SeedTenderAsync(WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"EXT-{Guid.NewGuid():N}", "Lucrări de construcții", "Caiet de sarcini pentru lucrări de construcții",
            ["45200000-1"], ["Montare de garduri"], 125_000m, "RON",
            "Primăria Test", "Iași", DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30),
            DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private async Task GrantAdministratorRole(string email)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = (await users.FindByEmailAsync(email))!;
        IdentityResult result = await users.AddToRoleAsync(user, MarketplaceRoles.Administrator);
        Assert.True(result.Succeeded, string.Join(' ', result.Errors.Select(error => error.Description)));
    }

    private static object SupplierRequest(string email) => new
    {
        tradingName = "Firma gratuită SRL",
        companyRegistrationNumber = $"J00/{Guid.NewGuid():N}",
        taxIdentifier = "RO123",
        contactPerson = "Persoană contact",
        contactEmail = email,
        phoneNumber = "0700000000",
        website = (string?)null,
        address = "Strada Companiei 1",
        city = "Iași",
        county = "Iași",
        latitude = (decimal?)null,
        longitude = (decimal?)null,
        description = "Profil de companie pentru acordarea unei generări gratuite"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record PricingPreview(decimal EstimatedValueEur, int PriceEur, int FreeGenerationsRemaining, bool ShadowMode);
    private sealed record AutoBidResponse(bool Ready, bool ShadowMode, bool FreeGeneration);
}
