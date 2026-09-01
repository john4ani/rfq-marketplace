using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

// Regression: the JWT handler maps inbound claims by default (MapInboundClaims), so the "sub" claim is
// remapped to ClaimTypes.NameIdentifier. CompanyVaultController previously read only "sub", which
// resolved to Guid.Empty for every authenticated request and made every vault endpoint return 404.
// The controller now reads both claim names (the same dual fallback ProfilesController uses).
[Collection(IntegrationTestGroup.Name)]
public sealed class CompanyVaultAuthorizationEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public CompanyVaultAuthorizationEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task VaultEndpointsResolveUserIdFromMappedJwtClaims()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"vault-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);

        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid userId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Create the company (SupplierProfile + Owner CompanyMembership) through the real flow.
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", SupplierProfileRequest(email))).StatusCode);

        // The user's own vault must be reachable — it 404'd while the user id resolved to Guid.Empty.
        HttpResponseMessage vault = await client.GetAsync($"/api/v1/companies/{userId}/vault");
        Assert.Equal(HttpStatusCode.OK, vault.StatusCode);
        Assert.Empty(await vault.Content.ReadFromJsonAsync<object[]>() ?? []);
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie vault regresie", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru testul de autorizare a vault-ului"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
