using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

// The auto-bid pipeline persists generated .docx/.xlsx as storage references (local://… in
// Development). The SEAP bid-file endpoint must resolve those references server-side and stream the
// bytes with an attachment disposition — browsers cannot navigate local:// or s3:// schemes directly,
// which used to make the „Descarcă propunerea tehnică / lista de cantități" downloads empty.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapBidFileDownloadEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public SeapBidFileDownloadEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task GeneratedProposalFilesCanBeDownloadedAfterAutoBid()
    {
        using WebApplicationFactory<Program> downloadFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Features:AutoBiddingEnabled"] = "true",
                    ["AI:UseMockProvider"] = "true"
                }));
        });
        using HttpClient client = downloadFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        string email = $"seap-bid-download-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        Guid tenderId = await SeedTenderAsync();

        // Shadow mode: generation runs immediately, without Stripe checkout.
        HttpResponseMessage autoBid = await client.PostAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid", null);
        Assert.Equal(HttpStatusCode.OK, autoBid.StatusCode);
        Assert.True((await autoBid.Content.ReadFromJsonAsync<AutoBidResponse>())!.Ready);

        BidStatus status = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/bid-status"))
            .Content.ReadFromJsonAsync<BidStatus>())!;
        Assert.Equal("BidGenerated", status.Status);
        Assert.False(string.IsNullOrWhiteSpace(status.TechnicalProposalUrl));
        Assert.False(string.IsNullOrWhiteSpace(status.BillOfQuantitiesUrl));

        await AssertDownloadAsync(client, tenderId, "TechnicalProposal", ".docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        await AssertDownloadAsync(client, tenderId, "BillOfQuantities", ".xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public async Task BidFileDownloadRequiresAuthentication()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Guid tenderId = await SeedTenderAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/bid-file?kind=TechnicalProposal");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> SeedTenderAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"EXT-{Guid.NewGuid():N}", "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private static async Task AssertDownloadAsync(
        HttpClient client, Guid tenderId, string kind, string extension, string contentType)
    {
        HttpResponseMessage download = await client.GetAsync(
            $"/api/v1/seap/tenders/{tenderId}/bid-file?kind={kind}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(contentType, download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(extension, download.Content.Headers.ContentDisposition?.FileName);
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, $"{kind} should not be empty");
        // .docx/.xlsx are ZIP archives and start with the PK magic bytes.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record AutoBidResponse(bool Ready, bool ShadowMode);
    private sealed record BidStatus(string Status, string? ProposalTitle, string? TechnicalProposalUrl, string? BillOfQuantitiesUrl);
}
