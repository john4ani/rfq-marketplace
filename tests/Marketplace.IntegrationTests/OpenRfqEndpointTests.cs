using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class OpenRfqEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] IasiCounty = ["Iași"];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    public OpenRfqEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task SupplierSeesOnlyPublishedRequestsMatchingCategoryAndDeliveryCounty()
    {
        const string password = "password";
        string buyerEmail = $"open-buyer-{Guid.NewGuid():N}@example.test";
        TokenResponse buyer = await RegisterAndLogin("buyer", buyerEmail, password);
        Guid buyerId = UserId(buyer.AccessToken);
        Authorize(buyer.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName = "Cumpărător Open", buyerType = "Company", companyName = "Constructor Test SRL",
            taxIdentifier = "RO123", phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;
        Guid parentCategoryId = categories.Single(category => category.Id == categoryId).ParentId!.Value;
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", Request(categoryId));
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);
        RfqResponse broadRfq = (await (await _client.PostAsJsonAsync("/api/v1/rfqs", BroadRequest(parentCategoryId)))
            .Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{broadRfq.Id}/publish", new { })).StatusCode);

        string supplierEmail = $"open-supplier-{Guid.NewGuid():N}@example.test";
        TokenResponse supplier = await RegisterAndLogin("supplier", supplierEmail, password);
        Guid supplierId = UserId(supplier.AccessToken);
        Authorize(supplier.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}", new {
            tradingName = "Depozit Open", companyRegistrationNumber = "J00/2/2026", taxIdentifier = "RO456",
            contactPerson = "Furnizor Test", contactEmail = supplierEmail, phoneNumber = "0711111111",
            website = (string?)null, address = "Strada Furnizor 1", city = "Iași", county = "Iași",
            latitude = (decimal?)null, longitude = (decimal?)null, description = "Materiale"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}/categories",
            new { categoryIds = new[] { categoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}/delivery-settings", new {
            normalDeliveryRadiusKm = 50, maximumDeliveryRadiusKm = 100, minimumOrderValue = (decimal?)null,
            nationwideDelivery = false, counties = IasiCounty
        })).StatusCode);

        HttpResponseMessage response = await _client.GetAsync("/api/v1/open-rfqs?search=Ciment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        OpenRfqResponse[] matches = JsonSerializer.Deserialize<OpenRfqResponse[]>(json, WebJson)!;
        OpenRfqResponse match = Assert.Single(matches, item => item.Id == rfq.Id);
        Assert.Equal("Iași", match.County);
        Assert.Equal("Constructor Test SRL", match.BuyerDisplayName);
        JsonElement responseItem = JsonDocument.Parse(json).RootElement[0];
        Assert.False(responseItem.TryGetProperty("deliveryAddress", out _));
        Assert.False(responseItem.TryGetProperty("buyerNotes", out _));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/open-rfqs/{rfq.Id}")).StatusCode);

        OpenRfqResponse[] allMatches = (await _client.GetFromJsonAsync<OpenRfqResponse[]>("/api/v1/open-rfqs"))!;
        Assert.Contains(allMatches, item => item.Id == broadRfq.Id);
    }

    private async Task<TokenResponse> RegisterAndLogin(string role, string email, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync($"/api/v1/auth/register/{role}", new { email, password })).StatusCode);
        return (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static object Request(Guid categoryId) => new {
        title = "Ciment pentru fundație", projectName = "Casă Iași", description = "Necesar pentru fundație",
        primaryCategoryId = categoryId, deliveryAddress = "Adresă privată 10", deliveryLocality = "Iași", county = "Iași",
        supplierSearchRadiusKm = 30, latitude = (decimal?)null, longitude = (decimal?)null,
        preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
        estimatedBudget = 5000m, buyerNotes = "Notă privată",
        items = new[] { new { productName = "Ciment", manufacturerOrBrand = (string?)null, specification = "40 kg",
            quantity = 20m, unit = "Bag", alternativesAccepted = true, buyerNotes = (string?)null } }
    };

    private static object BroadRequest(Guid parentCategoryId) => new {
        source = "Assisted", inputType = "ProjectDescription", title = "Materiale pentru proiect general",
        projectName = (string?)null, description = "Necesarul urmează să fie detaliat cu furnizorul.",
        primaryCategoryId = parentCategoryId, deliveryAddress = "Adresă privată 10",
        deliveryLocality = "Iași", county = "Iași", supplierSearchRadiusKm = 30,
        latitude = (decimal?)null, longitude = (decimal?)null, preferredDeliveryDate = (DateOnly?)null,
        quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
        estimatedBudget = (decimal?)null, buyerNotes = (string?)null, allowSplit = true,
        deliveryRequired = true, items = Array.Empty<object>()
    };

    private static Guid UserId(string token)
    {
        string part = token.Split('.')[1];
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug);
    private sealed record RfqResponse(Guid Id);
    private sealed record OpenRfqResponse(Guid Id, string County, string BuyerDisplayName);
}
