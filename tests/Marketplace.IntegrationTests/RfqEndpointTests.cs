using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class RfqEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public RfqEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task BuyerCanCreateUpdateAndPublishOwnRequest()
    {
        string email = $"rfq-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName="Cumpărător Test",buyerType="Company",companyName="Test SRL",taxIdentifier="RO123",
            phoneNumber="0700000000",defaultCity="Iași",defaultCounty="Iași"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;
        object draft = Request(categoryId, "Materiale inițiale", 10, includeSecondItem: true);
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", draft);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Draft", rfq.Status);
        Assert.Equal(30, rfq.SupplierSearchRadiusKm);
        Assert.Equal(2, rfq.Items.Length);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/rfqs/{rfq.Id}", Request(categoryId, "Materiale actualizate", 25))).StatusCode);
        RfqResponse published = (await (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Published", published.Status);
        DateTimeOffset extendedDeadline = DateTimeOffset.UtcNow.AddDays(4);
        RfqResponse extended = (await (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{rfq.Id}/extend-deadline",
            new { quotationDeadline = extendedDeadline })).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(extendedDeadline, extended.QuotationDeadline, TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{rfq.Id}/extend-deadline",
            new { quotationDeadline = DateTimeOffset.UtcNow.AddDays(3) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/v1/rfqs/{rfq.Id}", draft)).StatusCode);
        RfqResponse[] mine = (await _client.GetFromJsonAsync<RfqResponse[]>("/api/v1/rfqs/mine"))!;
        Assert.Contains(mine, item => item.Id == rfq.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/rfqs/{Guid.NewGuid()}")).StatusCode);
        RfqResponse cancelled = (await (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{rfq.Id}/cancel", new { })).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Cancelled", cancelled.Status);
    }

    private static object Request(Guid categoryId, string title, decimal quantity, bool includeSecondItem = false) => new {
        title,projectName="Renovare",description="Necesar test",primaryCategoryId=categoryId,
        deliveryAddress="Strada Test 1",deliveryLocality="Iași",county="Iași",latitude=(decimal?)null,longitude=(decimal?)null,
        preferredDeliveryDate=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        quotationDeadline=DateTimeOffset.UtcNow.AddDays(2),alternativeProductsAccepted=true,estimatedBudget=5000m,buyerNotes=(string?)null,
        items=(includeSecondItem
            ? new[]{new{productName="Ciment",manufacturerOrBrand=(string?)null,specification="40 kg",quantity,unit="Bag",alternativesAccepted=true,buyerNotes=(string?)null},
                new{productName="Nisip",manufacturerOrBrand=(string?)null,specification="spălat",quantity=2m,unit="Tonne",alternativesAccepted=true,buyerNotes=(string?)null}}
            : new[]{new{productName="Ciment",manufacturerOrBrand=(string?)null,specification="40 kg",quantity,unit="Bag",alternativesAccepted=true,buyerNotes=(string?)null}})
    };
    private static Guid UserId(string token) {
        string part=token.Split('.')[1]; string json=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(part.PadRight(part.Length+(4-part.Length%4)%4,'=').Replace('-','+').Replace('_','/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }
    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug);
    private sealed record RfqResponse(
        Guid Id, string Status, int SupplierSearchRadiusKm, DateTimeOffset QuotationDeadline, RfqItemResponse[] Items);
    private sealed record RfqItemResponse(Guid Id, string ProductName, decimal Quantity);
}
