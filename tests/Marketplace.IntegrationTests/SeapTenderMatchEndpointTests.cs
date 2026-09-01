using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Application.Seap;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class SeapTenderMatchEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public SeapTenderMatchEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task ChatClarifiesThenExtractsFiltersAndSavesProfileOnCompany()
    {
        using WebApplicationFactory<Program> aiFactory = AiFactory();
        using HttpClient client = aiFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Guid companyId = await AuthenticateSupplierAsync(client);

        // The chat first asks exactly one clarification.
        MatchResponse started = (await (await client.PostAsJsonAsync("/api/v1/seap/tender-match",
            new { input = "Căutăm licitații pentru lucrări de drumuri și poduri în Moldova." }))
            .Content.ReadFromJsonAsync<MatchResponse>())!;
        Assert.True(started.ClarificationNeeded);
        Assert.Equal("În ce județe cauți licitații?", started.ClarificationQuestion);

        // Answering one question yields the rigid database parameters.
        MatchResponse ready = (await (await client.PostAsJsonAsync("/api/v1/seap/tender-match",
            new
            {
                input = "Căutăm licitații pentru lucrări de drumuri și poduri în Moldova.",
                turns = new[] { new { question = started.ClarificationQuestion, answer = "În Moldova." } }
            })).Content.ReadFromJsonAsync<MatchResponse>())!;
        Assert.False(ready.ClarificationNeeded);
        Assert.Equal(["45233000", "45221111"], ready.ExtractedCpvPrefixes);
        Assert.Equal(["IS", "SV", "BT"], ready.ExtractedCountyCodes);
        Assert.Equal(["Iași", "Suceava", "Botoșani"], ready.ExtractedCounties);

        // Confirmation persists the filters on the represented company profile (codes are normalized).
        HttpResponseMessage saved = await client.PutAsJsonAsync("/api/v1/seap/tender-match/profile",
            new { companyId, cpvPrefixes = SavedCpvPrefixes, counties = SavedCountyCodes });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        ProfileResponse profile = (await (await client.GetAsync($"/api/v1/suppliers/{companyId}"))
            .Content.ReadFromJsonAsync<ProfileResponse>())!;
        Assert.Equal(["45221111", "45233000"], profile.SeapCpvPrefixes);
        Assert.Equal(["Botoșani", "Iași", "Suceava"], profile.SeapCounties);
    }

    [Fact]
    public async Task MissingAiConfigurationReturnsServiceUnavailable()
    {
        using WebApplicationFactory<Program> unavailableFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AiIntake:ApiKey"] = string.Empty })));
        using HttpClient client = unavailableFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await AuthenticateSupplierAsync(client);

        AvailabilityResponse availability = (await (await client.GetAsync("/api/v1/seap/tender-match/availability"))
            .Content.ReadFromJsonAsync<AvailabilityResponse>())!;
        Assert.False(availability.ModelAvailable);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/seap/tender-match",
            new { input = "O descriere suficient de detaliată pentru testul de indisponibilitate." });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }


    [Fact]
    public async Task CannotSaveFiltersOnAnotherUsersCompany()
    {
        using WebApplicationFactory<Program> aiFactory = AiFactory();
        using HttpClient owner = aiFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Guid companyId = await AuthenticateSupplierAsync(owner);

        string email = $"seap-match-other-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        using HttpClient other = aiFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Created,
            (await other.PostAsJsonAsync("/api/v1/auth/register/buyer", new { email, password })).StatusCode);
        TokenResponse login = (await (await other.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync("/api/v1/seap/tender-match/profile",
            new { companyId, cpvPrefixes = SingleCpvPrefix, counties = Array.Empty<string>() })).StatusCode);
    }

    private WebApplicationFactory<Program> AiFactory() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISeapTenderMatchInterpreter>();
            services.AddScoped<ISeapTenderMatchInterpreter, FakeSeapTenderMatchInterpreter>();
        }));

    private static readonly string[] SavedCpvPrefixes = ["45233000", "45221111"];
    private static readonly string[] SavedCountyCodes = ["IS", "SV", "BT"];
    private static readonly string[] SingleCpvPrefix = ["45"];

    private static async Task<Guid> AuthenticateSupplierAsync(HttpClient client)
    {
        string email = $"seap-match-{Guid.NewGuid():N}@example.test";
        const string password = "password";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        Guid userId = UserId(login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", new
        {
            tradingName = "SC Potrivire Test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
            contactPerson = "Persoană contact", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
            address = "Strada 1", city = "Iași", county = "Iași"
        })).StatusCode);
        return userId;
    }

    private static Guid UserId(string token)
    {
        string part = token.Split('.')[1];
        string json = Encoding.UTF8.GetString(Convert.FromBase64String(
            part.PadRight(part.Length + (4 - part.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(json).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed class FakeSeapTenderMatchInterpreter : ISeapTenderMatchInterpreter
    {
        public bool IsAvailable => true;

        public Task<SeapTenderMatchResult> InterpretAsync(SeapTenderMatchRequest request, CancellationToken cancellationToken)
        {
            bool clarified = request.Turns.Count > 0;
            return Task.FromResult(new SeapTenderMatchResult(
                !clarified, clarified ? null : "În ce județe cauți licitații?",
                ["45233000", "45221111"],
                clarified ? ["IS", "SV", "BT"] : [],
                clarified ? ["Iași", "Suceava", "Botoșani"] : [],
                clarified ? "Am identificat interes pentru Lucrări de drumuri și poduri în zona Moldovei." : null,
                "TestAI", "test-model", "test-v1", 100, 50));
        }
    }

    private sealed record TokenResponse(string AccessToken);
    private sealed record AvailabilityResponse(bool Enabled, bool ModelAvailable);
    private sealed record MatchResponse(bool ClarificationNeeded, string? ClarificationQuestion,
        string[] ExtractedCpvPrefixes, string[] ExtractedCountyCodes, string[] ExtractedCounties, string? SummaryText);
    private sealed record ProfileResponse(Guid Id, string[] SeapCpvPrefixes, string[] SeapCounties);
}
