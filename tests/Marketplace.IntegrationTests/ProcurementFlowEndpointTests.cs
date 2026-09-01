using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class ProcurementFlowEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private const string Password = "password";
    private static readonly string[] IasiCounty = ["Iași"];
    private readonly MarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProcurementFlowEndpointTests(MarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
    }

    [Fact]
    public async Task SeparateBuyerAndSupplierCompleteTheRequestToOfferCircuit()
    {
        string buyerEmail = $"flow-buyer-{Guid.NewGuid():N}@example.test";
        TokenResponse buyer = await RegisterAndLogin("buyer", buyerEmail);
        Guid buyerId = UserId(buyer.AccessToken);
        Authorize(buyer.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new
        {
            displayName = "Buyer Flow", buyerType = "Company", companyName = "Buyer Flow SRL",
            taxIdentifier = "ROFLOWBUYER", phoneNumber = "0700000000", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);

        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new
        {
            title = "Flux complet de achiziție", projectName = "Proiect test", description = "Cerere pentru două produse",
            primaryCategoryId = categoryId, deliveryAddress = "Strada Test 1", deliveryLocality = "Iași", county = "Iași",
            supplierSearchRadiusKm = 30, latitude = (decimal?)null, longitude = (decimal?)null,
            preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            allowSplit = true, deliveryRequired = false, estimatedBudget = 2_000m, buyerNotes = (string?)null,
            items = new[]
            {
                new { productName = "Produs disponibil", manufacturerOrBrand = (string?)null, specification = "Specificație A", quantity = 2m, unit = "Piece", alternativesAccepted = true, buyerNotes = (string?)null },
                new { productName = "Produs indisponibil", manufacturerOrBrand = (string?)null, specification = "Specificație B", quantity = 1m, unit = "Piece", alternativesAccepted = true, buyerNotes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);

        string supplierEmail = $"flow-supplier-{Guid.NewGuid():N}@example.test";
        TokenResponse supplier = await RegisterAndLogin("supplier", supplierEmail);
        Guid supplierId = UserId(supplier.AccessToken);
        Authorize(supplier.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}", new
        {
            tradingName = "Provider Flow SRL", companyRegistrationNumber = "J00/FLOW/2026", taxIdentifier = "ROFLOWPROVIDER",
            contactPerson = "Provider Flow", contactEmail = supplierEmail, phoneNumber = "0711111111", website = (string?)null,
            address = "Strada Furnizor 2", city = "Iași", county = "Iași", latitude = (decimal?)null,
            longitude = (decimal?)null, description = "Provider test"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}/categories",
            new { categoryIds = new[] { categoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{supplierId}/delivery-settings", new
        {
            normalDeliveryRadiusKm = 50, maximumDeliveryRadiusKm = 100, minimumOrderValue = (decimal?)null,
            nationwideDelivery = false, counties = IasiCounty
        })).StatusCode);

        OpenRfqResponse[] open = (await _client.GetFromJsonAsync<OpenRfqResponse[]>("/api/v1/open-rfqs"))!;
        Assert.Contains(open, request => request.Id == rfq.Id);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/open-rfqs/{rfq.Id}")).StatusCode);
        // RFQs are user-owned after Task 3A; another authenticated user must not be
        // able to discover a buyer's request through the buyer-only endpoint.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/rfqs/{rfq.Id}")).StatusCode);

        HttpResponseMessage saved = await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new
        {
            pricingMode = "Itemized", coverage = "Partial", taxTreatment = "VatIncluded", vatRate = 19m,
            deliveryTerms = "AddedToTotal", totalPrice = (decimal?)null, deliveryIncluded = true, deliveryCost = 30m,
            deliveryDays = 3, validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            paymentTerms = "La livrare", notes = "O poziție nu este disponibilă",
            items = new[]
            {
                new { rfqItemId = rfq.Items[0].Id, quantity = 2m, unitPrice = 100m, productDescription = "Produs A", available = true },
                new { rfqItemId = rfq.Items[1].Id, quantity = 1m, unitPrice = 999m, productDescription = "Indisponibil", available = false }
            }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        OfferResponse draft = (await saved.Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal(200m, draft.ItemsTotal);
        Assert.Equal(230m, draft.Total);
        HttpResponseMessage submittedResponse =
            await _client.PostAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submittedResponse.StatusCode);
        Assert.Equal("Submitted", (await submittedResponse.Content.ReadFromJsonAsync<OfferResponse>())!.Status);

        Authorize(buyer.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new { })).StatusCode);
        NotificationResponse[] notifications =
            (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(notifications, notification =>
            notification.Type == "OfferReceived" && notification.Link == $"/rfqs/{rfq.Id}/offers");
        BuyerOfferResponse comparison = Assert.Single(
            (await _client.GetFromJsonAsync<BuyerOfferResponse[]>($"/api/v1/rfqs/{rfq.Id}/offers"))!);
        Assert.Equal("Provider Flow SRL", comparison.SupplierName);
        Assert.Equal(230m, comparison.Offer.Total);
        Assert.Equal("Partial", comparison.Offer.Coverage);
        Assert.Equal(1, comparison.CoverageDetails.CoveredPositionCount);
        Assert.Single(comparison.CoverageDetails.MissingRfqItemIds);
        Assert.Contains(comparison.Offer.Items, item => !item.Available);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{rfq.Id}/offers/{comparison.Offer.Id}/select", new { })).StatusCode);

        await AssertCompetingRequestChangesAreRejected(rfq.Id);
    }

    private async Task AssertCompetingRequestChangesAreRejected(Guid rfqId)
    {
        await using AsyncServiceScope firstScope = _factory.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = _factory.Services.CreateAsyncScope();
        MarketplaceDbContext firstDatabase = firstScope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        MarketplaceDbContext secondDatabase = secondScope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        Marketplace.Domain.Rfqs.Rfq first = await firstDatabase.Rfqs.SingleAsync(rfq => rfq.Id == rfqId);
        Marketplace.Domain.Rfqs.Rfq second = await secondDatabase.Rfqs.SingleAsync(rfq => rfq.Id == rfqId);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        first.ExtendQuotationDeadline(now.AddDays(3), now);
        second.ExtendQuotationDeadline(now.AddDays(4), now);
        await firstDatabase.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDatabase.SaveChangesAsync());
    }

    private async Task<TokenResponse> RegisterAndLogin(string role, string email)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync($"/api/v1/auth/register/{role}", new { email, password = Password })).StatusCode);
        return (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Guid UserId(string token)
    {
        string part = token.Split('.')[1];
        string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
            part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId);
    private sealed record RfqResponse(Guid Id, RfqItemResponse[] Items);
    private sealed record RfqItemResponse(Guid Id);
    private sealed record OpenRfqResponse(Guid Id);
    private sealed record OfferResponse(Guid Id, string Status, string Coverage, decimal ItemsTotal, decimal Total, OfferItemResponse[] Items);
    private sealed record OfferItemResponse(bool Available);
    private sealed record CoverageResponse(int CoveredPositionCount, Guid[] MissingRfqItemIds);
    private sealed record BuyerOfferResponse(string SupplierName, OfferResponse Offer, CoverageResponse CoverageDetails);
    private sealed record NotificationResponse(string Type, string Link);
}
