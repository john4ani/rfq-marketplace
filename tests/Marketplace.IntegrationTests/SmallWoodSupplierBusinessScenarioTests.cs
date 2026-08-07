using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class SmallWoodSupplierBusinessScenarioTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] SuceavaCounty = ["Suceava"];
    private static readonly decimal[] ExpectedLineTotals = [2880m, 900m];
    private readonly HttpClient _client;

    public SmallWoodSupplierBusinessScenarioTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    [Fact]
    public async Task ItemizedTimberOrderCalculatesCommercialTotalAndReachesBuyer()
    {
        const string password = "password";
        string suffix = Guid.NewGuid().ToString("N");
        string providerEmail = $"wood-provider-{suffix}@example.test";
        string buyerEmail = $"wood-buyer-{suffix}@example.test";

        TokenResponse provider = await RegisterAndLogin("supplier", providerEmail, password);
        Guid providerId = UserId(provider.AccessToken);
        Authorize(provider.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{providerId}", new
        {
            tradingName = "Depozitul de Lemn Marginea",
            companyRegistrationNumber = "J33/100/2010",
            taxIdentifier = "RO12345678",
            contactPerson = "Furnizor Lemn",
            contactEmail = providerEmail,
            phoneNumber = "0711111111",
            website = (string?)null,
            address = "Strada Atelierului 1",
            city = "Marginea",
            county = "Suceava",
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            description = "Furnizor local de lemn și produse derivate"
        })).StatusCode);

        CategoryResponse[] categories =
            (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid woodCategoryId = categories.Single(category => category.Slug == "lemn-placi").Id;
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/v1/suppliers/{providerId}/categories", new { categoryIds = new[] { woodCategoryId } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/v1/suppliers/{providerId}/delivery-settings", new
            {
                normalDeliveryRadiusKm = 30,
                maximumDeliveryRadiusKm = 80,
                minimumOrderValue = 0m,
                nationwideDelivery = false,
                counties = SuceavaCounty
            })).StatusCode);

        TokenResponse buyer = await RegisterAndLogin("buyer", buyerEmail, password);
        Guid buyerId = UserId(buyer.AccessToken);
        Authorize(buyer.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new
        {
            displayName = "Client Cherestea",
            buyerType = "Individual",
            companyName = (string?)null,
            taxIdentifier = (string?)null,
            phoneNumber = "0722222222",
            defaultCity = "Marginea",
            defaultCounty = "Suceava"
        })).StatusCode);

        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new
        {
            title = "Cherestea și placaj pentru acoperiș",
            projectName = "Reparație acoperiș șură",
            description = "Comandă de lemn preluată din necesarul clientului.",
            primaryCategoryId = woodCategoryId,
            deliveryAddress = "Strada Clientului 10",
            deliveryLocality = "Marginea",
            county = "Suceava",
            supplierSearchRadiusKm = 30,
            latitude = (decimal?)null,
            longitude = (decimal?)null,
            preferredDeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            quotationDeadline = DateTimeOffset.UtcNow.AddDays(2),
            alternativeProductsAccepted = true,
            deliveryRequired = true,
            estimatedBudget = (decimal?)null,
            buyerNotes = "Confirmați stocul înainte de livrare.",
            items = new object[]
            {
                new { productName = "Cherestea rășinoase", manufacturerOrBrand = (string?)null,
                    specification = "Uscată, dimensiunile finale se confirmă", quantity = 2.4m,
                    unit = "CubicMetre", alternativesAccepted = true, buyerNotes = (string?)null,
                    categoryId = woodCategoryId },
                new { productName = "Placaj exterior", manufacturerOrBrand = (string?)null,
                    specification = "18 mm", quantity = 12m, unit = "Piece",
                    alternativesAccepted = true, buyerNotes = (string?)null, categoryId = woodCategoryId }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        RfqResponse rfq = (await created.Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsJsonAsync($"/api/v1/rfqs/{rfq.Id}/publish", new { })).StatusCode);

        Authorize(provider.AccessToken);
        OpenRfqResponse[] matches = (await _client.GetFromJsonAsync<OpenRfqResponse[]>("/api/v1/open-rfqs"))!;
        Assert.Contains(matches, match => match.Id == rfq.Id);

        HttpResponseMessage saved = await _client.PutAsJsonAsync($"/api/v1/open-rfqs/{rfq.Id}/offer", new
        {
            pricingMode = "Itemized",
            coverage = "Full",
            taxTreatment = "VatExcluded",
            vatRate = 19m,
            deliveryTerms = "AddedToTotal",
            deliveryIncluded = true,
            deliveryCost = 250m,
            deliveryDays = 3,
            validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            paymentTerms = "Plată la livrare",
            notes = "Prețurile sunt valabile pentru dimensiunile confirmate.",
            items = new object[]
            {
                new { rfqItemId = rfq.Items[0].Id, quantity = 2.4m, unitPrice = 1200m,
                    productDescription = "Cherestea rășinoase uscată", available = true },
                new { rfqItemId = rfq.Items[1].Id, quantity = 12m, unitPrice = 75m,
                    productDescription = "Placaj exterior 18 mm", available = true }
            }
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        OfferResponse offer = (await saved.Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal(3780m, offer.ItemsTotal);
        Assert.Equal(4030m, offer.NetTotal);
        Assert.Equal(765.70m, offer.VatAmount);
        Assert.Equal(4795.70m, offer.Total);
        Assert.True(offer.HasComparableTotal);
        Assert.Equal(ExpectedLineTotals, offer.Items.Select(item => item.LineTotal));

        OfferResponse submitted = (await (await _client.PostAsJsonAsync(
            $"/api/v1/open-rfqs/{rfq.Id}/offer/submit", new { })).Content.ReadFromJsonAsync<OfferResponse>())!;
        Assert.Equal("Submitted", submitted.Status);

        Authorize(buyer.AccessToken);
        BuyerOfferResponse[] comparison =
            (await _client.GetFromJsonAsync<BuyerOfferResponse[]>($"/api/v1/rfqs/{rfq.Id}/offers"))!;
        BuyerOfferResponse visibleOffer = Assert.Single(comparison);
        Assert.Equal("Depozitul de Lemn Marginea", visibleOffer.SupplierName);
        Assert.Equal(4795.70m, visibleOffer.Offer.Total);
        Assert.Equal(2, visibleOffer.CoverageDetails.CoveredPositionCount);
        Assert.Empty(visibleOffer.CoverageDetails.MissingRfqItemIds);
    }

    private async Task<TokenResponse> RegisterAndLogin(string accountType, string email, string password)
    {
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(
            $"/api/v1/auth/register/{accountType}", new { email, password })).StatusCode);
        return (await (await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
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
    private sealed record CategoryResponse(Guid Id, string Slug);
    private sealed record RfqItemResponse(Guid Id);
    private sealed record RfqResponse(Guid Id, RfqItemResponse[] Items);
    private sealed record OpenRfqResponse(Guid Id);
    private sealed record OfferItemResponse(decimal LineTotal);
    private sealed record OfferResponse(
        Guid Id,
        string Status,
        decimal ItemsTotal,
        decimal NetTotal,
        decimal VatAmount,
        decimal Total,
        bool HasComparableTotal,
        OfferItemResponse[] Items);
    private sealed record CoverageResponse(int CoveredPositionCount, Guid[] MissingRfqItemIds);
    private sealed record BuyerOfferResponse(string SupplierName, OfferResponse Offer, CoverageResponse CoverageDetails);
}
