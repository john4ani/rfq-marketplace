using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class OfferEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] BuyerAndSupplier = ["Buyer", "Supplier"];
    private readonly HttpClient _client;
    public OfferEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task ProviderCanSubmitAndBuyerCanSelectOffer()
    {
        string email = $"offer-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, accountTypes = BuyerAndSupplier })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid userId = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{userId}", new {
            displayName = "Buyer Offer", buyerType = "Company", companyName = "Buyer Offer SRL",
            taxIdentifier = "RO123", phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", new {
            tradingName = "Provider Offer SRL", companyRegistrationNumber = "J00/9/2026", taxIdentifier = "RO456",
            contactPerson = "Provider", contactEmail = email, phoneNumber = "0711111111", website = (string?)null,
            address = "Strada Test 2", city = "Iași", county = "Iași", latitude = (decimal?)null,
            longitude = (decimal?)null, description = "Test"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{userId}/categories",
            new { categoryIds = new[] { categoryId } })).StatusCode);

        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new {
            title = "Request with offer", projectName = "Project", description = "Test", primaryCategoryId = categoryId,
            deliveryAddress = "Private 1", deliveryLocality = "Iași", county = "Iași", supplierSearchRadiusKm = 30,
            latitude = (decimal?)null, longitude = (decimal?)null, preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            estimatedBudget = 1000m, buyerNotes = (string?)null,
            items = new[] { new { productName = "Product", manufacturerOrBrand = (string?)null, specification = "Spec",
                quantity = 10m, unit = "Piece", alternativesAccepted = true, buyerNotes = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);

        HttpResponseMessage saved = await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            deliveryCost = 50m, deliveryDays = 3, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            paymentTerms = "La livrare", notes = "Oferta test",
            items = new[] { new { rfqItemId = rfq.Items[0].Id, quantity = 10m, unitPrice = 90m,
                productDescription = "Produs echivalent", available = true } }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        OfferResponse offer = (await saved.Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal(950m, offer.Total);
        Assert.Equal("Submitted", (await (await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { }))
            .Content.ReadFromJsonAsync<OfferResponse>())!.Status);
        NotificationResponse[] notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        NotificationResponse received = Assert.Single(notifications, item => item.Type == "OfferReceived" && item.Link == $"/rfqs/{rfq.Id}/offers");
        Assert.False(received.IsRead);
        Assert.Equal(1, (await _client.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count"))!.Count);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/notifications/{received.Id}/read", new { })).StatusCode);
        Assert.Equal(0, (await _client.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count"))!.Count);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            deliveryCost = 40m, deliveryDays = 2, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            paymentTerms = "La livrare", notes = "Oferta revizuită",
            items = new[] { new { rfqItemId = rfq.Items[0].Id, quantity = 10m, unitPrice = 85m,
                productDescription = "Produs echivalent", available = true } }
        })).StatusCode);
        Assert.Equal("Revised", (await (await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { }))
            .Content.ReadFromJsonAsync<OfferResponse>())!.Status);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, item => item.Type == "OfferRevised" && item.Link == $"/rfqs/{rfq.Id}/offers");

        BuyerOfferResponse[] comparison = (await _client.GetFromJsonAsync<BuyerOfferResponse[]>($"/api/v1/rfqs/{rfq.Id}/offers"))!;
        Assert.Single(comparison);
        Assert.Equal("Provider Offer SRL", comparison[0].SupplierName);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/offers/{offer.Id}/select", new { })).StatusCode);
        RfqResponse selected = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfq.Id}"))!;
        Assert.Equal("OfferSelected", selected.Status);
        Assert.Equal(offer.Id, selected.SelectedOfferId);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/open-rfqs/{rfq.Id}")).StatusCode);
        OfferResponse selectedProviderOffer =
            (await _client.GetFromJsonAsync<OfferResponse>($"/api/v1/open-rfqs/{rfq.Id}/offer"))!;
        Assert.Equal("Selected", selectedProviderOffer.Status);
        const string reopenReason = "Oferta a fost selectată din greșeală și trebuie reevaluată.";
        HttpResponseMessage reopenedResponse = await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/reopen", new {
            reason = reopenReason,
            newQuotationDeadline = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.OK, reopenedResponse.StatusCode);
        RfqResponse reopened = (await reopenedResponse.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("ReceivingOffers", reopened.Status);
        Assert.Null(reopened.SelectedOfferId);
        OfferResponse restoredProviderOffer =
            (await _client.GetFromJsonAsync<OfferResponse>($"/api/v1/open-rfqs/{rfq.Id}/offer"))!;
        Assert.Equal("Revised", restoredProviderOffer.Status);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        NotificationResponse reopenedNotification = Assert.Single(
            notifications, item => item.Type == "RequestReopened" && item.Link == $"/open-requests/{rfq.Id}");
        Assert.Contains(reopenReason, reopenedNotification.Message);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            pricingMode = "Total", totalPrice = 1100m, deliveryCost = 0m, deliveryDays = 2,
            validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            paymentTerms = "La livrare", notes = "Acoperă întreaga cerere", items = Array.Empty<object>()
        })).StatusCode);
        OfferResponse quickTotal = (await (await _client.PostAsJsonAsync(
            $"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { })).Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal("Total", quickTotal.PricingMode);
        Assert.Equal(1100m, quickTotal.Total);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/offers/{quickTotal.Id}/select", new { })).StatusCode);
        HttpResponseMessage secondReopen = await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/reopen", new {
            reason = "A doua încercare nu trebuie permisă.",
            newQuotationDeadline = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, secondReopen.StatusCode);
        RfqResponse finallySelected = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfq.Id}"))!;
        Assert.Equal(1, finallySelected.ReopenCount);
        Assert.Equal("OfferSelected", finallySelected.Status);

        HttpResponseMessage generalCreated = await _client.PostAsJsonAsync("/api/v1/rfqs", new {
            title = "General request without lines", projectName = (string?)null, description = "General need",
            primaryCategoryId = categoryId, deliveryAddress = "Private 2", deliveryLocality = "Iași", county = "Iași",
            supplierSearchRadiusKm = 30, latitude = (decimal?)null, longitude = (decimal?)null,
            preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            estimatedBudget = (decimal?)null, buyerNotes = (string?)null, items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, generalCreated.StatusCode);
        RfqResponse generalRfq = (await generalCreated.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{generalRfq.Id}/publish", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{generalRfq.Id}/offer", new {
            pricingMode = "Total", totalPrice = 1250m, deliveryCost = 0m, deliveryDays = 4, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            paymentTerms = "La livrare", notes = "Ofertă generală", items = Array.Empty<object>()
        })).StatusCode);
        OfferResponse submittedGeneral = (await (await _client.PostAsJsonAsync(
            $"/api/v1/open-rfqs/{generalRfq.Id}/offer/submit", new { })).Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal("Submitted", submittedGeneral.Status);
        Assert.Equal(1250m, submittedGeneral.Total);
    }

    private static Guid UserId(string token) {
        string part = token.Split('.')[1];
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }
    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId);
    private sealed record RfqResponse(Guid Id, string Status, Guid? SelectedOfferId, int ReopenCount, RfqItemResponse[] Items);
    private sealed record RfqItemResponse(Guid Id);
    private sealed record OfferResponse(Guid Id, string Status, string PricingMode, decimal Total);
    private sealed record BuyerOfferResponse(string SupplierName, OfferResponse Offer);
    private sealed record NotificationResponse(Guid Id, string Type, string Link, string Message, bool IsRead);
    private sealed record UnreadCountResponse(int Count);
}
