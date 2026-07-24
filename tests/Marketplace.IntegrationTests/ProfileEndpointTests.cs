using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

public sealed class ProfileEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    public ProfileEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
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

        var profile = new { displayName = "Ion Test", buyerType = "Individual", companyName = (string?)null, taxIdentifier = (string?)null, phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași" };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{id}", profile)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/buyers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/buyers/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/v1/suppliers/{id}")).StatusCode);
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
            address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
            longitude = (decimal?)null, description = "Materiale"
        };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}", profile)).StatusCode);

        CategoryResponse[]? categories = await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories");
        Guid categoryId = Assert.Single(categories!, category => category.Name == "Izolații").Id;
        string customName = $"Piatră naturală {Guid.NewGuid():N}";
        CategoryResponse? customCategory = await (await _client.PostAsJsonAsync("/api/v1/categories", new { name = customName })).Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(customCategory);
        CategoryResponse? existingCategory = await (await _client.PostAsJsonAsync("/api/v1/categories", new { name = customName })).Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.Equal(customCategory.Id, existingCategory?.Id);
        Assert.True(existingCategory?.AlreadyExisted);

        string similarName = $"{customName}e";
        HttpResponseMessage similarWarning = await _client.PostAsJsonAsync("/api/v1/categories", new { name = similarName });
        Assert.Equal(HttpStatusCode.Conflict, similarWarning.StatusCode);
        SimilarCategoryResponse? warning = await similarWarning.Content.ReadFromJsonAsync<SimilarCategoryResponse>();
        Assert.Contains(warning!.SimilarCategories, category => category.Id == customCategory.Id);
        CategoryResponse? confirmedCategory = await (await _client.PostAsJsonAsync(
            "/api/v1/categories", new { name = similarName, confirmSimilar = true })).Content.ReadFromJsonAsync<CategoryResponse>();
        Assert.NotNull(confirmedCategory);
        Assert.NotEqual(customCategory.Id, confirmedCategory.Id);

        Guid[] selectedCategories = [categoryId, customCategory.Id];
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/categories", new { categoryIds = selectedCategories })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/categories", new { categoryIds = selectedCategories })).StatusCode);

        var delivery = new { normalDeliveryRadiusKm = 50, maximumDeliveryRadiusKm = 120, minimumOrderValue = 500m, nationwideDelivery = false, canFulfillMultiCategoryRequests = false, counties = new[] { "Iași", "Neamț" } };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/delivery-settings", delivery)).StatusCode);
        var updatedDelivery = new { normalDeliveryRadiusKm = 60, maximumDeliveryRadiusKm = 150, minimumOrderValue = 750m, nationwideDelivery = false, canFulfillMultiCategoryRequests = true, counties = new[] { "Iași", "Bacău" } };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}/delivery-settings", updatedDelivery)).StatusCode);
        DeliveryResponse? saved = await _client.GetFromJsonAsync<DeliveryResponse>($"/api/v1/suppliers/{id}/delivery-settings");
        Assert.Equal(150, saved?.MaximumDeliveryRadiusKm);
        Assert.Equal(2, saved?.Counties.Length);
        Assert.True(saved?.CanFulfillMultiCategoryRequests);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/suppliers/{Guid.NewGuid()}/delivery-settings")).StatusCode);

        // This suite uses the local integration database. Remove the categories created only
        // to exercise duplicate/similarity protection so they never leak into the product UI.
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MarketplaceDbContext database = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Guid[] temporaryCategoryIds = [customCategory.Id, confirmedCategory.Id];
        await database.SupplierCategories.Where(item => temporaryCategoryIds.Contains(item.CategoryId)).ExecuteDeleteAsync();
        await database.Categories.Where(category => temporaryCategoryIds.Contains(category.Id)).ExecuteDeleteAsync();
    }

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug, bool AlreadyExisted = false);
    private sealed record SimilarCategoryResponse(string Message, CategoryResponse[] SimilarCategories);
    private sealed record DeliveryResponse(int NormalDeliveryRadiusKm, int MaximumDeliveryRadiusKm, decimal? MinimumOrderValue, bool NationwideDelivery, bool CanFulfillMultiCategoryRequests, string[] Counties);
}
