using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class OfferEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly HttpClient _client;
    public OfferEndpointTests(MarketplaceWebApplicationFactory factory) =>
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task ProviderCanSubmitAndBuyerCanSelectOffer()
    {
        string buyerEmail = $"offer-buyer-{Guid.NewGuid():N}@example.test";
        string supplierEmail = $"offer-supplier-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer",
            new { email = buyerEmail, password })).StatusCode);
        TokenResponse buyerLogin = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = buyerEmail, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(buyerLogin.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName = "Buyer Offer", buyerType = "Company", companyName = "Buyer Offer SRL",
            taxIdentifier = "RO123", phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/supplier",
            new { email = supplierEmail, password })).StatusCode);
        TokenResponse supplierLogin = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = supplierEmail, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid supplierId = UserId(supplierLogin.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}", new {
            tradingName = "Provider Offer SRL", companyRegistrationNumber = "J00/9/2026", taxIdentifier = "RO456",
            contactPerson = "Provider", contactEmail = supplierEmail, phoneNumber = "0711111111", website = (string?)null,
            address = "Strada Test 2", city = "Iași", county = "Iași", latitude = (decimal?)null,
            longitude = (decimal?)null, description = "Test"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}/categories",
            new { categoryIds = new[] { categoryId } })).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new {
            title = "Request with offer", projectName = "Project", description = "Test", primaryCategoryId = categoryId,
            deliveryAddress = "Private 1", deliveryLocality = "Iași", county = "Iași", supplierSearchRadiusKm = 30,
            latitude = (decimal?)null, longitude = (decimal?)null, preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            deliveryRequired = true, estimatedBudget = 1000m, buyerNotes = (string?)null,
            items = new[] { new { productName = "Product", manufacturerOrBrand = (string?)null, specification = "Spec",
                quantity = 10m, unit = "Piece", alternativesAccepted = true, buyerNotes = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        HttpResponseMessage saved = await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            coverage = "Full", taxTreatment = "VatIncluded", vatRate = 19m, deliveryTerms = "AddedToTotal",
            deliveryIncluded = true, deliveryCost = 50m, deliveryDays = 3, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            paymentTerms = "La livrare", notes = "Oferta test",
            items = new[] { new { rfqItemId = rfq.Items[0].Id, quantity = 10m, unitPrice = 90m,
                productDescription = "Produs echivalent", available = true } }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        OfferResponse offer = (await saved.Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal(950m, offer.Total);
        Assert.Equal("Full", offer.Coverage);
        Assert.Equal("VatIncluded", offer.TaxTreatment);
        Assert.True(offer.VatAmount > 0);
        Assert.Equal("Submitted", (await (await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { }))
            .Content.ReadFromJsonAsync<OfferResponse>())!.Status);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
        NotificationResponse[] notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        NotificationResponse received = Assert.Single(notifications, item => item.Type == "OfferReceived" && item.Link == $"/rfqs/{rfq.Id}/offers");
        Assert.False(received.IsRead);
        Assert.True((await _client.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count"))!.Count >= 1);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/notifications/{received.Id}/read", new { })).StatusCode);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.True(Assert.Single(notifications, item => item.Id == received.Id).IsRead);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(
            $"/api/v1/offers/{offer.Id}/conversation/messages", new { body = "Putem clarifica termenul de livrare?" })).StatusCode);
        ConversationResponse conversation =
            (await _client.GetFromJsonAsync<ConversationResponse>($"/api/v1/offers/{offer.Id}/conversation"))!;
        ConversationMessageResponse message = Assert.Single(conversation.Messages);
        Assert.Equal("Putem clarifica termenul de livrare?", message.Body);
        Assert.True(message.IsMine);
        Assert.Equal(rfq.Id, conversation.RfqId);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _client.PostAsJsonAsync($"/api/v1/offers/{offer.Id}/conversation/read", new { })).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, item => item.Type == "MessageReceived" && item.Link == $"/offers/{offer.Id}/conversation");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/v1/offers/{Guid.NewGuid()}/conversation")).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            coverage = "Full", taxTreatment = "VatIncluded", vatRate = 19m, deliveryTerms = "AddedToTotal",
            deliveryIncluded = true, deliveryCost = 40m, deliveryDays = 2, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            paymentTerms = "La livrare", notes = "Oferta revizuită",
            items = new[] { new { rfqItemId = rfq.Items[0].Id, quantity = 10m, unitPrice = 85m,
                productDescription = "Produs echivalent", available = true } }
        })).StatusCode);
        Assert.Equal("Revised", (await (await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { }))
            .Content.ReadFromJsonAsync<OfferResponse>())!.Status);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, item => item.Type == "OfferRevised" && item.Link == $"/rfqs/{rfq.Id}/offers");

        BuyerOfferResponse[] comparison = (await _client.GetFromJsonAsync<BuyerOfferResponse[]>($"/api/v1/rfqs/{rfq.Id}/offers"))!;
        Assert.Single(comparison);
        Assert.Equal("Provider Offer SRL", comparison[0].SupplierName);
        Assert.Equal(1, comparison[0].CoverageDetails.CoveredPositionCount);
        Assert.Empty(comparison[0].CoverageDetails.MissingRfqItemIds);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/offers/{offer.Id}/select", new { })).StatusCode);
        RfqResponse selected = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfq.Id}"))!;
        Assert.Equal("OfferSelected", selected.Status);
        Assert.Equal(offer.Id, selected.SelectedOfferId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/open-rfqs/{rfq.Id}")).StatusCode);
        OfferResponse selectedProviderOffer =
            (await _client.GetFromJsonAsync<OfferResponse>($"/api/v1/open-rfqs/{rfq.Id}/offer"))!;
        Assert.Equal("Selected", selectedProviderOffer.Status);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
        const string reopenReason = "Oferta a fost selectată din greșeală și trebuie reevaluată.";
        HttpResponseMessage reopenedResponse = await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/reopen", new {
            reason = reopenReason,
            newQuotationDeadline = (DateTimeOffset?)null
        });
        Assert.Equal(HttpStatusCode.OK, reopenedResponse.StatusCode);
        RfqResponse reopened = (await reopenedResponse.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("ReceivingOffers", reopened.Status);
        Assert.Null(reopened.SelectedOfferId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        OfferResponse restoredProviderOffer =
            (await _client.GetFromJsonAsync<OfferResponse>($"/api/v1/open-rfqs/{rfq.Id}/offer"))!;
        Assert.Equal("Revised", restoredProviderOffer.Status);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        NotificationResponse reopenedNotification = Assert.Single(
            notifications, item => item.Type == "RequestReopened" && item.Link == $"/opportunities/{rfq.Id}?companyId={supplierId}");
        Assert.Contains(reopenReason, reopenedNotification.Message);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new {
            pricingMode = "Total", coverage = "Full", taxTreatment = "VatIncluded", vatRate = 19m, deliveryTerms = "IncludedInPrice",
            totalPrice = 1100m, deliveryIncluded = true, deliveryCost = 0m, deliveryDays = 2,
            validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)),
            paymentTerms = "La livrare", notes = "Acoperă întreaga cerere", items = Array.Empty<object>()
        })).StatusCode);
        OfferResponse quickTotal = (await (await _client.PostAsJsonAsync(
            $"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { })).Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal("Total", quickTotal.PricingMode);
        Assert.Equal(1100m, quickTotal.Total);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
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
        Assert.Equal("AwaitingDelivery", finallySelected.FulfillmentStatus);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/delivery/report", new { })).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
        RfqResponse deliveryReported = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfq.Id}"))!;
        Assert.Equal("DeliveryReported", deliveryReported.FulfillmentStatus);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, item => item.Type == "DeliveryReported" && item.Link == $"/rfqs/{rfq.Id}/offers");

        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/delivery/confirm", new { })).StatusCode);
        RfqResponse delivered = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfq.Id}"))!;
        Assert.Equal("Closed", delivered.Status);
        Assert.Equal("Delivered", delivered.FulfillmentStatus);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        notifications = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, item => item.Type == "DeliveryConfirmed" && item.Link == $"/opportunities/{rfq.Id}?companyId={supplierId}");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerLogin.AccessToken);
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
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supplierLogin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{generalRfq.Id}/offer", new {
            pricingMode = "Total", coverage = "Full", taxTreatment = "VatIncluded", vatRate = 19m, deliveryTerms = "IncludedInPrice",
            totalPrice = 1250m, deliveryCost = 0m, deliveryDays = 4, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
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
    private sealed record RfqResponse(
        Guid Id,
        string Status,
        Guid? SelectedOfferId,
        int ReopenCount,
        string FulfillmentStatus,
        RfqItemResponse[] Items);
    private sealed record RfqItemResponse(Guid Id);
    private sealed record OfferResponse(Guid Id, string Status, string PricingMode, string Coverage, string TaxTreatment, decimal VatAmount, decimal Total);
    private sealed record CoverageResponse(int CoveredPositionCount, Guid[] MissingRfqItemIds);
    private sealed record BuyerOfferResponse(string SupplierName, OfferResponse Offer, CoverageResponse CoverageDetails);
    private sealed record NotificationResponse(Guid Id, string Type, string Link, string Message, bool IsRead);
    private sealed record UnreadCountResponse(int Count);
    private sealed record ConversationResponse(Guid OfferId, Guid RfqId, ConversationMessageResponse[] Messages);
    private sealed record ConversationMessageResponse(Guid Id, string Body, DateTimeOffset CreatedAt, bool IsMine);
}
