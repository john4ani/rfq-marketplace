using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.IntegrationTests;

// The SEAP tender detail page reads GET /api/v1/seap/tenders/{id} (SeapTenderDetailsDto projection) and
// lets users download the original files the authority published via
// GET /api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}. The attachment endpoint must stream the
// stored file server-side (browsers cannot navigate local:// or s3:// references) and must never serve an
// attachment that does not belong to the requested tender (IDOR guard).
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapTenderDetailsEndpointTests : IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public SeapTenderDetailsEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task TenderDetailsExposeTheProjectionWithDerivedFieldsAndAttachments()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(20));

        SeapTenderDetailsDto details = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}"))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        Assert.Equal("Caiet de sarcini pentru lucrări de construcții", details.Description);
        // The simplified-procedure external id prefix derives the procedure type deterministically.
        Assert.Equal("Procedură simplificată", details.ProcedureType);
        // The deadline is still in the future → the procedure is open.
        Assert.Equal("În desfășurare", details.ProcedureState);
        // Detail fields the parser does not extract yet stay null so the SPA can show placeholders.
        Assert.Null(details.AwardCriterion);
        Assert.Null(details.FundingSource);
        Assert.Null(details.ParticipationGuarantee);
        Assert.Null(details.SummaryPoints);

        SeapTenderAttachmentRef attachment = Assert.Single(details.Attachments);
        Assert.Equal(attachmentId, attachment.Id);
        Assert.Equal("fisa-date.pdf", attachment.FileName);
        Assert.Equal($"api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}", attachment.DownloadUrl);
    }

    [Fact]
    public async Task OriginalAttachmentStreamsWithContentTypeAndFileName()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));

        HttpResponseMessage download = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith("fisa-date.pdf", download.Content.Headers.ContentDisposition?.FileName);
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal("fisa-date-pdf-content"u8.ToArray(), bytes);
    }

    [Fact]
    public async Task AttachmentDownloadDoesNotLeakAnotherTendersFiles()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));
        Guid otherTenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(5));

        // The attachment belongs to the first tender; requesting it through another tender must 404.
        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/seap/tenders/{otherTenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AttachmentDownloadRequiresAuthentication()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));

        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateConfiguredHost() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Features:AutoBiddingEnabled"] = "true",
                    ["AI:UseMockProvider"] = "true"
                }));
        });

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> host)
    {
        HttpClient client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        string email = $"seap-details-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private async Task<(Guid TenderId, Guid AttachmentId)> SeedTenderWithAttachmentAsync(DateTimeOffset deadline)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        SeapTender tender = SeapTender.Import(
            $"SCN{Guid.NewGuid():N}"[..12], "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), deadline, DateTimeOffset.UtcNow);
        await using var content = new MemoryStream("fisa-date-pdf-content"u8.ToArray());
        string reference = await storage.UploadAsync(
            "seap", $"seap/{tender.Id}", "fisa-date.pdf", content, "application/pdf", CancellationToken.None);
        SeapTenderAttachment attachment = tender.AddAttachment("fisa-date.pdf", reference, DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return (tender.Id, attachment.Id);
    }

    private async Task<Guid> SeedTenderAsync(DateTimeOffset deadline)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"SCN{Guid.NewGuid():N}"[..12], "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), deadline, DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
}
