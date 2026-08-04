using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class LiveMarketEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] BuyerAndSupplierRoles = ["Buyer", "Supplier"];
    private readonly HttpClient _client;
    private readonly HttpClient _publicClient;

    public LiveMarketEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        _publicClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    }

    [Fact]
    public async Task AuthenticatedUserCountsAsOnlineBeforePublicProfilesAreCompleted()
    {
        string unique = Guid.NewGuid().ToString("N");
        string email = $"presence-{unique}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, accountTypes = BuyerAndSupplierRoles })).StatusCode);
        TokenResponse? login = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        Guid id = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        LiveMarketSnapshot? snapshot = await _client.GetFromJsonAsync<LiveMarketSnapshot>("/api/v1/piata-live?profileType=Buyer");

        Assert.NotNull(snapshot);
        Assert.True(snapshot.OnlineUserCount >= 1);
        Assert.True(snapshot.OnlineBuyerCount >= 1);
        Assert.True(snapshot.OnlineSupplierCount >= 1);
        Assert.DoesNotContain(snapshot.Participants, participant => participant.Id == id);
    }

    [Fact]
    public async Task CompletedProfilesAppearPubliclyAndPresenceChangesImmediately()
    {
        string unique = Guid.NewGuid().ToString("N");
        string email = $"piata-{unique}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, accountTypes = BuyerAndSupplierRoles })).StatusCode);
        TokenResponse? login = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        Guid id = UserId(login.AccessToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        string testCounty = $"Județ Test {unique}";
        const string testCity = "Oraș Test";
        var buyer = new { displayName = $"Cumpărător {unique}", buyerType = "Individual", companyName = (string?)null, taxIdentifier = (string?)null, phoneNumber = "0700000000", defaultCity = testCity, defaultCounty = testCounty, marketRadiusKm = 30 };
        var supplier = new { tradingName = $"Furnizor {unique}", companyRegistrationNumber = "J00/1/2026", taxIdentifier = (string?)null, contactPerson = "Ion Test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null, address = "Strada Test 1", city = testCity, county = testCounty, latitude = (decimal?)null, longitude = (decimal?)null, description = (string?)null };
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/buyers/{id}", buyer)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/v1/suppliers/{id}", supplier)).StatusCode);

        Task<HttpResponseMessage>[] presenceRequests = Enumerable.Range(0, 12)
            .Select(index => index % 2 == 0
                ? _client.PostAsJsonAsync("/api/v1/piata-live/presence", new { })
                : _client.GetAsync("/api/v1/piata-live"))
            .ToArray();
        HttpResponseMessage[] concurrentResponses = await Task.WhenAll(presenceRequests);
        Assert.All(concurrentResponses, response => Assert.True(response.IsSuccessStatusCode));
        foreach (HttpResponseMessage response in concurrentResponses) response.Dispose();

        LiveMarketSnapshot? online = await _publicClient.GetFromJsonAsync<LiveMarketSnapshot>("/api/v1/piata-live");
        Assert.True(online!.OnlineUserCount >= 1);
        Assert.True(online.OnlineBuyerCount >= 1);
        Assert.True(online.OnlineSupplierCount >= 1);
        Assert.Contains(online.Participants, participant => participant.Id == id && participant.ProfileType == "Buyer" && participant.IsOnline);
        Assert.Contains(online.Participants, participant => participant.Id == id && participant.ProfileType == "Supplier" && participant.IsOnline);
        LiveMarketSnapshot? nearby = await _client.GetFromJsonAsync<LiveMarketSnapshot>("/api/v1/piata-live?profileType=Buyer");
        Assert.True(nearby!.IsLocationFiltered);
        Assert.Equal(testCity, nearby.FilterCity);
        Assert.Equal(testCounty, nearby.FilterCounty);
        Assert.Equal(30, nearby.ApproximateRadiusKm);
        Assert.All(nearby.Participants, participant => Assert.Equal(testCounty, participant.County));
        Assert.All(nearby.Participants.Where(participant => participant.Id == id), participant => Assert.True(participant.IsSameCity));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/v1/piata-live/presence")).StatusCode);
        LiveMarketSnapshot? offline = await _publicClient.GetFromJsonAsync<LiveMarketSnapshot>("/api/v1/piata-live");
        Assert.All(offline!.Participants.Where(participant => participant.Id == id), participant => Assert.False(participant.IsOnline));
    }

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record LiveMarketSnapshot(DateTimeOffset GeneratedAt, LiveParticipant[] Participants, int OnlineUserCount, int OnlineBuyerCount, int OnlineSupplierCount, bool IsLocationFiltered, string? FilterCity, string? FilterCounty, int? ApproximateRadiusKm);
    private sealed record LiveParticipant(Guid Id, string Name, string ProfileType, bool IsOnline, string City, string County, bool IsSameCity);
}
