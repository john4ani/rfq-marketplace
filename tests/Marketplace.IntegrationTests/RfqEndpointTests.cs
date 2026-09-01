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
public sealed class RfqEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory _factory;
    private readonly HttpClient _client;
    public RfqEndpointTests(MarketplaceWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
    }

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
        Assert.Equal("Manual", rfq.Source);
        Assert.Equal("StructuredForm", rfq.InputType);
        Assert.Equal("Draft", rfq.NeedStatus);
        Assert.Equal("Renovare", rfq.ProjectContext?.Name);
        Assert.Equal("Strada Test 1", rfq.DeliveryConstraints.Address);
        Assert.Equal(30, rfq.SupplierSearchRadiusKm);
        Assert.Equal(2, rfq.Items.Length);
        Assert.Equal("Service", rfq.Items[1].Kind);

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

        HttpResponseMessage outcomeCreated = await _client.PostAsJsonAsync(
            "/api/v1/rfqs", Request(categoryId, "Cerere cu rezultat", 5));
        RfqResponse outcomeRfq = (await outcomeCreated.Content.ReadFromJsonAsync<RfqResponse>())!;
        await _client.PostAsJsonAsync($"/api/v1/rfqs/{outcomeRfq.Id}/publish", new { });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{outcomeRfq.Id}/outcome", new { outcome = "Other", reason = " " })).StatusCode);
        RfqResponse recorded = (await (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{outcomeRfq.Id}/outcome",
            new { outcome = "PurchasedElsewhere", reason = "Am găsit produsul local." })).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Closed", recorded.Status);
        Assert.Equal("PurchasedElsewhere", recorded.Outcome);
        Assert.Equal("Am găsit produsul local.", recorded.OutcomeReason);
        AuditResponse[] outcomeHistory = (await _client.GetFromJsonAsync<AuditResponse[]>($"/api/v1/rfqs/{outcomeRfq.Id}/history"))!;
        Assert.Equal("Created,Published,OutcomeRecorded", string.Join(',', outcomeHistory.Select(entry => entry.Type)));

        RfqResponse reminderRfq = (await (await _client.PostAsJsonAsync(
            "/api/v1/rfqs", Request(categoryId, "Cerere expirată", 3))).Content.ReadFromJsonAsync<RfqResponse>())!;
        await _client.PostAsJsonAsync($"/api/v1/rfqs/{reminderRfq.Id}/publish", new { });
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            MarketplaceDbContext database = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
            await database.Rfqs.Where(item => item.Id == reminderRfq.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.QuotationDeadline, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        NotificationResponse[] reminders = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(reminders, item => item.Type == "ProcurementOutcomeRequested" && item.Link == $"/rfqs/{reminderRfq.Id}/offers");
        reminders = (await _client.GetFromJsonAsync<NotificationResponse[]>("/api/v1/notifications"))!;
        Assert.Single(reminders, item => item.Type == "ProcurementOutcomeRequested" && item.Link == $"/rfqs/{reminderRfq.Id}/offers");

        RfqResponse cancelled = (await (await _client.PostAsJsonAsync(
            $"/api/v1/rfqs/{rfq.Id}/cancel", new { })).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Cancelled", cancelled.Outcome);
        AuditResponse[] history = (await _client.GetFromJsonAsync<AuditResponse[]>($"/api/v1/rfqs/{rfq.Id}/history"))!;
        Assert.Equal("Created,Updated,Published,DeadlineExtended,Cancelled,OutcomeRecorded", string.Join(',', history.Select(entry => entry.Type)));
    }

    [Fact]
    public async Task PersonalLocationContextIsPersistedAndReloaded()
    {
        string email = $"rfq-loc-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName = "Cumpărător Test", buyerType = "Company", companyName = "Test SRL", taxIdentifier = "RO123",
            phoneNumber = "0700000000", defaultAddress = "Strada Test 1", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;

        // Regression: the RfqLocationContext CLR default is "Personal" (0), while the store default is "Other" (2).
        // Without an explicit sentinel, EF omits "Personal" from inserts and the store default "Other" is persisted.
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new {
            source = "Manual", inputType = "StructuredForm", title = "Cerere personală", projectName = (string?)null, description = "Necesar test",
            primaryCategoryId = categoryId, locationContext = "Personal", buyingCompanyId = (Guid?)null,
            deliveryAddress = "Strada Test 1", deliveryLocality = "Iași", county = "Iași", latitude = (decimal?)null, longitude = (decimal?)null,
            preferredDeliveryDate = (DateOnly?)null, quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            estimatedBudget = 5000m, buyerNotes = (string?)null, items = new[] {
                new { kind = "Product", productName = "Ciment", manufacturerOrBrand = (string?)null, specification = "40 kg", quantity = 10m, unit = "Bag", alternativesAccepted = true, buyerNotes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid rfqId = (await created.Content.ReadFromJsonAsync<RfqResponse>())!.Id;

        RfqResponse reloaded = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfqId}"))!;
        Assert.Equal("Personal", reloaded.LocationContext);
    }

    [Fact]
    public async Task BuyerCanDeleteOnlyUnpublishedDraft()
    {
        string email = $"rfq-del-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName = "Cumpărător Test", buyerType = "Company", companyName = "Test SRL", taxIdentifier = "RO123",
            phoneNumber = "0700000000", defaultAddress = "Strada Test 1", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;

        RfqResponse draft = (await (await _client.PostAsJsonAsync("/api/v1/rfqs", Request(categoryId, "Ciornă de șters", 10))).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal("Draft", draft.Status);
        HttpResponseMessage deleteDraft = await _client.DeleteAsync($"/api/v1/rfqs/{draft.Id}");
        Assert.True(deleteDraft.IsSuccessStatusCode, await deleteDraft.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/rfqs/{draft.Id}")).StatusCode);

        RfqResponse published = (await (await _client.PostAsJsonAsync("/api/v1/rfqs", Request(categoryId, "Ciornă publicată", 10))).Content.ReadFromJsonAsync<RfqResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/v1/rfqs/{published.Id}/publish", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.DeleteAsync($"/api/v1/rfqs/{published.Id}")).StatusCode);
    }

    [Fact]
    public async Task CompanyLocationContextPersistsWhenBuyerRepresentsTheCompany()
    {
        string email = $"rfq-co-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid buyerId = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{buyerId}", new {
            displayName = "Cumpărător Test", buyerType = "Company", companyName = "Test SRL", taxIdentifier = "RO123",
            phoneNumber = "0700000000", defaultAddress = "Strada Test 1", defaultCity = "Iași", defaultCounty = "Iași"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{buyerId}", new {
            tradingName = "Furnizor Test SRL", companyRegistrationNumber = "J40/999/2024", taxIdentifier = (string?)null,
            contactPerson = "Reprezentant Test", contactEmail = email, phoneNumber = "0700000000",
            website = (string?)null, address = "Strada Furnizor 2", city = "Suceava", county = "Suceava",
            country = "România", marketRadiusKm = 50, description = (string?)null
        })).StatusCode);
        CategoryResponse[] categories = (await _client.GetFromJsonAsync<CategoryResponse[]>("/api/v1/categories"))!;
        Guid categoryId = categories.First(category => category.ParentId is not null).Id;

        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/v1/rfqs", new {
            source = "Manual", inputType = "StructuredForm", title = "Cerere pentru firmă", projectName = (string?)null, description = "Necesar test",
            primaryCategoryId = categoryId, locationContext = "Company", buyingCompanyId = buyerId,
            deliveryAddress = "Strada Furnizor 2", deliveryLocality = "Suceava", county = "Suceava", latitude = (decimal?)null, longitude = (decimal?)null,
            preferredDeliveryDate = (DateOnly?)null, quotationDeadline = DateTimeOffset.UtcNow.AddDays(2), alternativeProductsAccepted = true,
            estimatedBudget = 5000m, buyerNotes = (string?)null, items = new[] {
                new { kind = "Product", productName = "Ciment", manufacturerOrBrand = (string?)null, specification = "40 kg", quantity = 10m, unit = "Bag", alternativesAccepted = true, buyerNotes = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Guid rfqId = (await created.Content.ReadFromJsonAsync<RfqResponse>())!.Id;

        RfqResponse reloaded = (await _client.GetFromJsonAsync<RfqResponse>($"/api/v1/rfqs/{rfqId}"))!;
        Assert.Equal("Company", reloaded.LocationContext);
        Assert.Equal(buyerId, reloaded.BuyingCompanyId);
    }

    private static object Request(Guid categoryId, string title, decimal quantity, bool includeSecondItem = false) => new {
        source="Manual",inputType="StructuredForm",title,projectName="Renovare",description="Necesar test",primaryCategoryId=categoryId,
        deliveryAddress="Strada Test 1",deliveryLocality="Iași",county="Iași",latitude=(decimal?)null,longitude=(decimal?)null,
        preferredDeliveryDate=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        quotationDeadline=DateTimeOffset.UtcNow.AddDays(2),alternativeProductsAccepted=true,estimatedBudget=5000m,buyerNotes=(string?)null,
        items=(includeSecondItem
            ? new[]{new{kind="Product",productName="Ciment",manufacturerOrBrand=(string?)null,specification="40 kg",quantity,unit="Bag",alternativesAccepted=true,buyerNotes=(string?)null},
                new{kind="Service",productName="Transport",manufacturerOrBrand=(string?)null,specification="Livrare la adresă",quantity=2m,unit="Other",alternativesAccepted=true,buyerNotes=(string?)null}}
            : new[]{new{kind="Product",productName="Ciment",manufacturerOrBrand=(string?)null,specification="40 kg",quantity,unit="Bag",alternativesAccepted=true,buyerNotes=(string?)null}})
    };
    private static Guid UserId(string token) {
        string part=token.Split('.')[1]; string json=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(part.PadRight(part.Length+(4-part.Length%4)%4,'=').Replace('-','+').Replace('_','/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }
    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string Slug);
    private sealed record RfqResponse(
        Guid Id, string Status, string Source, string InputType, string NeedStatus, string LocationContext, Guid? BuyingCompanyId,
        ProjectContextResponse? ProjectContext, DeliveryConstraintsResponse DeliveryConstraints,
        string? Outcome, string? OutcomeReason, int SupplierSearchRadiusKm, DateTimeOffset QuotationDeadline, RfqItemResponse[] Items);
    private sealed record ProjectContextResponse(string? Name, string? Description);
    private sealed record DeliveryConstraintsResponse(string Address);
    private sealed record RfqItemResponse(Guid Id, string ProductName, decimal Quantity, string Kind);
    private sealed record AuditResponse(Guid Id, string Type);
    private sealed record NotificationResponse(Guid Id, string Type, string Link);
}
