using System.Text;
using Microsoft.Extensions.DependencyInjection;

using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Local-first developer experience guardrails: the local-file storage provider used when
// Storage:UseLocalStorage=true, and the SEAP golden-payload fallback used when the live endpoint is
// unreachable in Development (Seap:LocalFallbackEnabled).
public sealed class LocalFirstDeveloperTests
{
    [Fact]
    public async Task LocalFileStorageServiceWritesFilesUnderConfiguredRoot()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "compaspot-storage-" + Guid.NewGuid().ToString("N"));
        try
        {
            IOptions<LocalStorageOptions> options = Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot });
            LocalFileStorageService storage = new(options, NullLogger<LocalFileStorageService>.Instance);
            using MemoryStream content = new(Encoding.UTF8.GetBytes("pdf-content"));

            string reference = await storage.UploadAsync("vault", "vault/company-123", "oferta.pdf", content, "application/pdf", CancellationToken.None);

            Assert.StartsWith("local://vault/company-123/", reference, StringComparison.Ordinal);
            string written = Assert.Single(Directory.GetFiles(Path.Combine(tempRoot, "vault", "company-123")));
            Assert.EndsWith("-oferta.pdf", Path.GetFileName(written), StringComparison.Ordinal);
            Assert.Equal("pdf-content", await File.ReadAllTextAsync(written));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SeapApiClientFallsBackToGoldenPayloadWhenLiveEndpointIsUnavailable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "compaspot-seap-mocks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "mock.json"), MockEnvelopeJson);
            IOptions<SeapOptions> options = Options.Create(new SeapOptions
            {
                BaseUrl = "https://e-licitatie.ro",
                LocalFallbackEnabled = true,
                LocalFallbackDirectory = tempDir,
                MaxNoticesPerPoll = 20
            });

            SeapApiClient client = new(new ThrowingHttpClientFactory(), options, NullLogger<SeapApiClient>.Instance);

            IReadOnlyCollection<SeapTenderNoticeDto> notices = await client.FetchRecentNoticesAsync(CancellationToken.None);

            SeapTenderNoticeDto notice = Assert.Single(notices);
            Assert.Equal("CN-FALLBACK-1", notice.Id);
            Assert.Contains("fallback", notice.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1000m, notice.EstimatedValue);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DevelopmentConfigurationWiresLocalStorageAndRealClaudeProvider()
    {
        using MarketplaceWebApplicationFactory factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.IsType<LocalFileStorageService>(scope.ServiceProvider.GetRequiredService<IStorageService>());
        Assert.IsType<AnthropicClaudeProvider>(scope.ServiceProvider.GetRequiredService<ILlmProvider>());
    }

    private const string MockEnvelopeJson = """
        {
          "total": 1,
          "items": [
            {
              "cNoticeId": 1,
              "noticeId": 2,
              "procedureId": 3,
              "noticeNo": "CN-FALLBACK-1",
              "sysNoticeTypeId": 2,
              "sysNoticeState": { "id": 2, "text": "Publicat", "localeKey": null, "apiActionResult": null },
              "sysProcedureState": { "id": 5, "text": "Atribuita", "localeKey": null, "apiActionResult": null },
              "contractingAuthorityNameAndFN": "999 - AUTORITATE LOCALĂ TEST",
              "contractTitle": "Anunț fallback local (mock)",
              "sysAcquisitionContractType": { "id": 1, "text": "Furnizare", "localeKey": null, "apiActionResult": null },
              "sysProcedureType": { "id": 9, "text": "Altele", "localeKey": null, "apiActionResult": null },
              "cpvCodeAndName": "33652100-6 - Medicamente",
              "estimatedValueRon": 1000,
              "noticeStateDate": "2019-04-02T01:35:01+03:00",
              "minTenderReceiptDeadline": null,
              "maxTenderReceiptDeadline": "2019-05-09T15:00:00+03:00"
            }
          ],
          "searchTooLong": false
        }
        """;

    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new ThrowingHandler());
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated connection failure.");
    }
}
