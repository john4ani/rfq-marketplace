using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class ProfileEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ProfileEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

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

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
