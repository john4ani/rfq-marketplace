using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

public sealed class AuthenticationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] BuyerAndSupplierRoles = ["Buyer", "Supplier"];
    private readonly HttpClient _client;

    public AuthenticationEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task BuyerCanRegisterLoginRefreshAndLogout()
    {
        string email = $"buyer-{Guid.NewGuid():N}@example.test";
        const string password = "simple123";

        using HttpResponseMessage registration = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, accountTypes = BuyerAndSupplierRoles });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using HttpResponseMessage duplicateRegistration = await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password });
        string duplicateProblem = await duplicateRegistration.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, duplicateRegistration.StatusCode);
        Assert.Contains("Există deja un utilizator", duplicateProblem, StringComparison.Ordinal);

        using HttpResponseMessage login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers, header => header.Key == "Set-Cookie" && header.Value.Any(value => value.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase)));
        TokenResponse? buyerToken = await login.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(buyerToken);
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", buyerToken.AccessToken);

        LinkedAccount[]? profiles = await _client.GetFromJsonAsync<LinkedAccount[]>("/api/v1/auth/profiles");
        Assert.Equal(2, profiles?.Length);

        using HttpResponseMessage refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        using HttpResponseMessage logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record LinkedAccount(Guid UserId, string Email, string AccountType);
}
