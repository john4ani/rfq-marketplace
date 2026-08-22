using System.Net;
using System.Text;
using System.Text.Json;
using Marketplace.Infrastructure.AiIntake;
using Marketplace.Infrastructure.SupplierDiscovery;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

public sealed class SupplierDiscoveryWebEvidenceCollectorTests
{
    [Fact]
    public async Task StopsAfterTheLocalQueryWhenItHasTenSourceBackedCompanies()
    {
        RecordingHandler handler = new([CompletedResponse(Evidence(10))]);
        WebEvidenceCollector collector = CreateCollector(handler);

        WebEvidenceResult result = await collector.CollectAsync(Request(), CancellationToken.None);

        Assert.Single(result.Responses);
        Assert.Single(handler.RequestBodies);
        Assert.Contains("local registry query", handler.RequestBodies.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunsTheCountyQueryWhenTheLocalPoolHasFewerThanTenCompanies()
    {
        RecordingHandler handler = new([CompletedResponse(Evidence(9)), CompletedResponse(Evidence(2, start: 10))]);
        WebEvidenceCollector collector = CreateCollector(handler);

        WebEvidenceResult result = await collector.CollectAsync(Request(), CancellationToken.None);

        Assert.Equal(2, result.Responses.Count);
        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("local registry query", handler.RequestBodies[0], StringComparison.Ordinal);
        Assert.Contains("county registry query", handler.RequestBodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebRequestUsesOnlyApprovedBusinessDirectoryDomains()
    {
        RecordingHandler handler = new([CompletedResponse(Evidence(10))]);
        WebEvidenceCollector collector = CreateCollector(handler);

        await collector.CollectAsync(Request(), CancellationToken.None);

        using JsonDocument request = JsonDocument.Parse(handler.RequestBodies.Single());
        string[] domains = request.RootElement.GetProperty("tools")[0].GetProperty("filters").GetProperty("allowed_domains")
            .EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
        Assert.Equal(["termene.ro", "listafirme.ro", "metricbiz.ro", "firme.info", "firme-on-line.ro", "topfirme.com"], domains);
        Assert.Equal("low", request.RootElement.GetProperty("tools")[0].GetProperty("search_context_size").GetString());
    }

    private static WebEvidenceCollector CreateCollector(RecordingHandler handler) => new(new OpenAiResponsesTransport(
        new HttpClient(handler),
        Options.Create(new AiIntakeOptions { ApiKey = "test-key", Model = "gpt-5.6-luna" }),
        NullLogger<OpenAiResponsesTransport>.Instance));

    private static SupplierDiscoveryEvidenceRequest Request() => new(
        "Lemn și produse din lemn", [], "Marginea", "Suceava", 47.81m, 25.81m, 100,
        ["Lemn rotund"], ["CAEN 0220"], "lemn rotund", ["local registry query", "county registry query"], "guidance");

    private static string Evidence(int count, int start = 1) => JsonSerializer.Serialize(new
    {
        searchIntent = new { productOrService = "lemn", primaryActivity = "CAEN 0220", secondaryActivity = "", location = "Marginea" },
        evidence = Enumerable.Range(start, count).Select(index => new
        {
            companyName = $"Firma {index} SRL", locality = "Marginea", county = "Suceava", website = "", phone = "", email = "",
            activityEvidence = "CAEN 0220", caenCode = "0220", productEvidence = "lemn rotund", sourceUrls = new[] { $"https://termene.ro/firma/firma-{index}-srl" }
        })
    });

    private static string CompletedResponse(string output) => JsonSerializer.Serialize(new
    {
        status = "completed",
        output = new object[]
        {
            new { type = "web_search_call", action = new { sources = Array.Empty<object>() } },
            new { type = "message", content = new[] { new { type = "output_text", text = output } } }
        },
        usage = new { input_tokens = 1, output_tokens = 1 }
    });

    private sealed class RecordingHandler(IReadOnlyCollection<string> responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }
}
