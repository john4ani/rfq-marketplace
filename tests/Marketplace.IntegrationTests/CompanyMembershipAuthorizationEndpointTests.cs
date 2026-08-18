using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class CompanyMembershipAuthorizationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public CompanyMembershipAuthorizationEndpointTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task ActiveOwnerAndRepresentativeCanManageExplicitCompanySettingsWhileOthersCannot()
    {
        const string password = "password";
        Registration ownerRegistration = await Register("supplier-owner", "/api/v1/auth/register/supplier", password);
        Guid ownerId = ownerRegistration.UserId;
        string ownerEmail = ownerRegistration.Email;
        using HttpClient owner = AuthenticatedClient(ownerRegistration.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}", SupplierRequest(ownerEmail, "Firma proprietar"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/suppliers/{ownerId}")).StatusCode);

        CategoryResponse[] catalog = (await owner.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = catalog.First(category => category.ParentId is not null && !catalog.Any(other => other.ParentId == category.Id)).Id;
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/categories", new { categoryIds = new[] { categoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/delivery-settings", DeliveryRequest())).StatusCode);

        Registration representativeRegistration = await Register("company-representative", "/api/v1/auth/register", password);
        Guid representativeId = representativeRegistration.UserId;
        Registration secondOwnerRegistration = await Register("second-company-owner", "/api/v1/auth/register/supplier", password);
        Guid secondOwnerId = secondOwnerRegistration.UserId;
        string secondOwnerEmail = secondOwnerRegistration.Email;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.CompanyMemberships.Add(CompanyMembership.Create(representativeId, ownerId, CompanyMembershipRole.Representative, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using HttpClient secondOwner = AuthenticatedClient(secondOwnerRegistration.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await secondOwner.PutAsJsonAsync($"/api/v1/suppliers/{secondOwnerId}", SupplierRequest(secondOwnerEmail, "A doua firmă"))).StatusCode);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            db.CompanyMemberships.Add(CompanyMembership.Create(representativeId, secondOwnerId, CompanyMembershipRole.Representative, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        using HttpClient representative = AuthenticatedClient(representativeRegistration.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await representative.GetAsync($"/api/v1/suppliers/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await representative.GetAsync($"/api/v1/suppliers/{secondOwnerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await representative.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/categories", new { categoryIds = new[] { categoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await representative.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/delivery-settings", DeliveryRequest())).StatusCode);

        Registration unrelatedRegistration = await Register("buyer-only", "/api/v1/auth/register/buyer", password);
        using HttpClient unrelated = AuthenticatedClient(unrelatedRegistration.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.GetAsync($"/api/v1/suppliers/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/categories", new { categoryIds = new[] { categoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.PutAsJsonAsync($"/api/v1/suppliers/{ownerId}/delivery-settings", DeliveryRequest())).StatusCode);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            CompanyMembership membership = await db.CompanyMemberships.SingleAsync(item => item.CompanyId == ownerId && item.UserId == representativeId);
            membership.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();

            Assert.True(await db.SupplierProfiles.AnyAsync(profile => profile.Id == ownerId));
            Assert.True(await db.SupplierCategories.AnyAsync(item => item.SupplierId == ownerId));
            Assert.True(await db.SupplierDeliverySettings.AnyAsync(item => item.SupplierId == ownerId));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await representative.GetAsync($"/api/v1/suppliers/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await representative.GetAsync($"/api/v1/suppliers/{ownerId}/categories")).StatusCode);
    }

    private async Task<Registration> Register(string prefix, string route, string password)
    {
        string email = $"{prefix}-{Guid.NewGuid():N}@example.test";
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(route, new { email, password })).StatusCode);
        HttpResponseMessage login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        TokenResponse token = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        return new(UserId(token.AccessToken), email, token.AccessToken);
    }

    private HttpClient AuthenticatedClient(string accessToken)
    {
        HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static object SupplierRequest(string email, string name) => new
    {
        tradingName = name, companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Persoană contact", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Companiei 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil de companie pentru autorizare"
    };

    private static object DeliveryRequest() => new { normalDeliveryRadiusKm = 50, maximumDeliveryRadiusKm = 100, minimumOrderValue = 100m, nationwideDelivery = false, counties = new[] { "Iași" } };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record Registration(Guid UserId, string Email, string AccessToken);
    private sealed record CategoryResponse(Guid Id, Guid VerticalId, Guid? ParentId, string Name, string Slug);
}
