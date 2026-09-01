using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class AuthenticationEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private static readonly string[] BuyerAndSupplierRoles = ["Buyer", "Supplier"];
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationEndpointTests(MarketplaceWebApplicationFactory factory)
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

        CurrentUser? currentUser = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");
        Assert.NotNull(currentUser);
        Assert.NotEqual(Guid.Empty, currentUser.UserId);
        Assert.Equal(email, currentUser.Email);
        Assert.Equal(BuyerAndSupplierRoles, currentUser.Roles.Order().ToArray());

        LinkedAccount[]? profiles = await _client.GetFromJsonAsync<LinkedAccount[]>("/api/v1/auth/profiles");
        Assert.Equal(2, profiles?.Length);

        using HttpResponseMessage refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        using HttpResponseMessage logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task UserCanRegisterWithoutSelectingBuyerOrSupplierRole()
    {
        string email = $"user-{Guid.NewGuid():N}@example.test";
        const string password = "simple123";

        Assert.Equal(HttpStatusCode.Created,
            (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password })).StatusCode);

        TokenResponse? login = await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        CurrentUser? currentUser = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/auth/me");
        Assert.NotNull(currentUser);
        Assert.Empty(currentUser.Roles);
        Assert.Empty((await _client.GetFromJsonAsync<LinkedAccount[]>("/api/v1/auth/profiles"))!);
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
        Assert.False(response!.EmailTransportEnabled);
        Assert.False(response!.EmailSent);

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

    [Fact]
    public async Task EmailConfirmationReportsWhetherDeliveryActuallyOccurred()
    {
        string email = $"confirmation-{Guid.NewGuid():N}@example.test";
        const string password = "confirmation-password";
        Assert.Equal(
            HttpStatusCode.Created,
            (await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password })).StatusCode);
        TokenResponse login = (await (await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using HttpResponseMessage requested = await _client.PostAsJsonAsync("/api/v1/auth/request-email-confirmation", new { });
        EmailConfirmationResponse? response = await requested.Content.ReadFromJsonAsync<EmailConfirmationResponse>();

        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        Assert.NotNull(response);
        Assert.False(response!.EmailSent);
        Assert.False(response.AlreadyConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Theory]
    [InlineData("Buyer", "Supplier")]
    [InlineData("Supplier", "Buyer")]
    public async Task SignedInAccountCanAddTheOtherProfileRole(string initialRole, string addedRole)
    {
        string email = $"add-profile-{Guid.NewGuid():N}@example.test";
        const string password = "simple123";
        Assert.Equal(
            HttpStatusCode.Created,
            (await _client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { email, password, accountTypes = new[] { initialRole } })).StatusCode);

        TokenResponse? login = await (
            await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(login);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        using HttpResponseMessage addition = await _client.PostAsJsonAsync($"/api/v1/auth/profiles/{addedRole}", new { });
        TokenResponse? updated = await addition.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.Equal(HttpStatusCode.OK, addition.StatusCode);
        Assert.NotNull(updated);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", updated.AccessToken);

        LinkedAccount[] profiles = (await _client.GetFromJsonAsync<LinkedAccount[]>("/api/v1/auth/profiles"))!;
        Assert.Equal(["Buyer", "Supplier"], profiles.Select(profile => profile.AccountType).Order().ToArray());

        Guid userId = profiles[0].UserId;
        HttpResponseMessage profileSave = addedRole == "Buyer"
            ? await _client.PutAsJsonAsync($"/api/v1/buyers/{userId}", new
            {
                displayName = "Buyer Added Later",
                buyerType = "Individual",
                companyName = (string?)null,
                taxIdentifier = (string?)null,
                phoneNumber = "0700000000",
                defaultCity = "Iași",
                defaultCounty = "Iași",
                marketRadiusKm = 30
            })
            : await _client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", new
            {
                tradingName = "Provider Added Later SRL",
                companyRegistrationNumber = "J00/100/2026",
                taxIdentifier = "RO100",
                contactPerson = "Test Provider",
                contactEmail = email,
                phoneNumber = "0711111111",
                website = (string?)null,
                address = "Strada Test 1",
                city = "Iași",
                county = "Iași",
                latitude = (decimal?)null,
                longitude = (decimal?)null,
                description = (string?)null
            });

        Assert.Equal(HttpStatusCode.OK, profileSave.StatusCode);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CurrentUser(Guid UserId, string Email, string[] Roles);
    private sealed record LinkedAccount(Guid UserId, string Email, string AccountType);
    private sealed record DevelopmentTokenResponse(string? Token, bool EmailTransportEnabled = false, bool EmailSent = false);
    private sealed record EmailConfirmationResponse(bool EmailSent, bool AlreadyConfirmed, string? Token);
}
