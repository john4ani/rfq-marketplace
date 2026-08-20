using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class ProfileEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ProfileEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILocationGeocoder>();
            services.AddScoped<ILocationGeocoder, FakeLocationGeocoder>();
        })).CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
    }

    [Fact]
    public async Task BuyerCanCreateReadAndUpdateOnlyOwnProfile()
    {
        string email = $"profile-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse? login = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        string[] parts = login.AccessToken.Split('.');
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        Guid id = Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var profile = new { displayName = "Ion Test", buyerType = "Individual", companyName = (string?)null, taxIdentifier = (string?)null, phoneNumber = "0700000000", defaultAddress = "Strada Test 1", defaultCity = "Iași", defaultCounty = "Iași", defaultCountry = "România", marketRadiusKm = 30 };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{id}", profile)).StatusCode);
        HttpResponseMessage savedBuyer = await _client.GetAsync($"/api/v1/buyers/{id}");
        Assert.Equal(HttpStatusCode.OK, savedBuyer.StatusCode);
        BuyerResponse saved = (await savedBuyer.Content.ReadFromJsonAsync<BuyerResponse>())!;
        Assert.Equal(30, saved.MarketRadiusKm);
        Assert.NotNull(saved.DefaultLatitude);
        Assert.NotNull(saved.DefaultLongitude);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/buyers/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/suppliers/{id}")).StatusCode);
    }

    [Fact]
    public async Task SupplierCanConfigureCategoriesAndDeliveryArea()
    {
        string email = $"supplier-settings-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse? login = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        Guid id = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var profile = new
        {
            tradingName = "Depozit Test", companyRegistrationNumber = "J00/1/2026", taxIdentifier = "RO123",
            contactPerson = "Ion Test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
            address = "Strada Test 1", city = "Iași", county = "Iași", country = "România", marketRadiusKm = 30,
            description = "Materiale"
        };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}", profile)).StatusCode);
        DeliveryResponse? defaultDelivery = await _client.GetFromJsonAsync<DeliveryResponse>($"/api/v1/suppliers/{id}/delivery-settings");
        Assert.Equal(30, defaultDelivery?.NormalDeliveryRadiusKm);
        Assert.Equal(30, defaultDelivery?.MaximumDeliveryRadiusKm);

        CategoryResponse[]? categories = await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories");
        Guid categoryId = Assert.Single(categories!, category => category.Name == "Izolații").Id;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.PostAsJsonAsync("/api/v1/categories", new { name = $"Categorie {Guid.NewGuid():N}" })).StatusCode);

        Guid[] selectedCategories = [categoryId];
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/categories", new { categoryIds = selectedCategories })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/categories", new { categoryIds = selectedCategories })).StatusCode);

        var delivery = new { normalDeliveryRadiusKm = 50, maximumDeliveryRadiusKm = 120, minimumOrderValue = 500m, nationwideDelivery = false, counties = new[] { "Iași", "Neamț" } };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/delivery-settings", delivery)).StatusCode);
        var updatedDelivery = new { normalDeliveryRadiusKm = 60, maximumDeliveryRadiusKm = 150, minimumOrderValue = 750m, nationwideDelivery = false, counties = new[] { "Iași", "Bacău" } };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/delivery-settings", updatedDelivery)).StatusCode);
        DeliveryResponse? saved = await _client.GetFromJsonAsync<DeliveryResponse>($"/api/v1/suppliers/{id}/delivery-settings");
        Assert.Equal(150, saved?.MaximumDeliveryRadiusKm);
        Assert.Equal(2, saved?.Counties.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/suppliers/{Guid.NewGuid()}/delivery-settings")).StatusCode);

    }

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record BuyerResponse(decimal? DefaultLatitude, decimal? DefaultLongitude, int MarketRadiusKm);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug, bool AlreadyExisted = false);
    private sealed record DeliveryResponse(int NormalDeliveryRadiusKm, int MaximumDeliveryRadiusKm, decimal? MinimumOrderValue, bool NationwideDelivery, string[] Counties);

    private sealed class FakeLocationGeocoder : ILocationGeocoder
    {
        public Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new GeographicLocation(input.Address, input.Locality, input.County, input.Country, 47.7m, 25.8m));
    }
}
