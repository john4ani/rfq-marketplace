using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

public sealed class AuthenticationEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] BuyerAndSupplierRoles = ["Buyer", "Supplier"];
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task DevelopmentHttpRequestsAreNotRedirectedAwayFromBearerAuthentication()
    {
        using HttpClient httpClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });

        using HttpResponseMessage response = await httpClient.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    [Fact]
    public async Task BuyerCanCompletePasswordRecoveryInDevelopment()
    {
        string email = $"recovery-{Guid.NewGuid():N}@example.test";
        const string password = "initial-password";
        const string newPassword = "replacement-password";
        Assert.Equal(
            HttpStatusCode.Created,
            (await _client.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);

        using HttpResponseMessage requested = await _client.PostAsJsonAsync(
            "/api/v1/auth/request-password-reset",
            new { email });
        DevelopmentTokenResponse? response = await requested.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response?.Token));

        using HttpResponseMessage reset = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new { email, token = response.Token, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password })).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = newPassword })).StatusCode);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record LinkedAccount(Guid UserId, string Email, string AccountType);
    private sealed record DevelopmentTokenResponse(string? Token);
}
