using System.Net;
using System.Text.Json;
using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Seap;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Guards the on-demand SEAP search request contract: the search must query the configured notice types
// (2 = CN, 17 = SCN — where commune/town tenders such as "PRIMARIA COMUNEI POIANA DAMBOVITA" live), the
// requested page, and the publication-date window.
public sealed class SeapApiClientTests
{
    [Fact]
    public async Task FetchNoticePageSendsConfiguredNoticeTypesPageAndDateWindow()
    {
        string? capturedBody = null;
        StubHttpClientFactory factory = new(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"total\":0,\"items\":[],\"searchTooLong\":false}")
            };
        });
        SeapApiClient client = new(
            factory,
            Options.Create(new SeapOptions
            {
                BaseUrl = "https://e-licitatie.ro",
                NoticesPath = "api-pub/NoticeCommon/GetCNoticeList/",
                RequestTimeoutSeconds = 30,
                NoticeTypeIds = [2, 17]
            }),
            NullLogger<SeapApiClient>.Instance);

        await client.FetchNoticePageAsync(2, 500, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None);

        Assert.NotNull(capturedBody);
        using JsonDocument json = JsonDocument.Parse(capturedBody!);
        JsonElement root = json.RootElement;
        Assert.Equal([2, 17],
            root.GetProperty("sysNoticeTypeIds").EnumerateArray().Select(item => item.GetInt32()).ToArray());
        Assert.Equal(2, root.GetProperty("pageIndex").GetInt32());
        Assert.Equal(500, root.GetProperty("pageSize").GetInt32());
        Assert.Equal("2026-01-01", root.GetProperty("startPublicationDate").GetString());
        Assert.Equal("2026-12-31", root.GetProperty("endPublicationDate").GetString());
    }

    [Fact]
    public async Task FetchNoticePageOmitsTheDateWindowWhenAllHistoryIsRequested()
    {
        string? capturedBody = null;
        StubHttpClientFactory factory = new(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"total\":0,\"items\":[],\"searchTooLong\":false}")
            };
        });
        SeapApiClient client = new(
            factory,
            Options.Create(new SeapOptions { BaseUrl = "https://e-licitatie.ro", NoticeTypeIds = [2, 17] }),
            NullLogger<SeapApiClient>.Instance);

        await client.FetchNoticePageAsync(0, 500, null, null, CancellationToken.None);

        using JsonDocument json = JsonDocument.Parse(capturedBody!);
        Assert.False(json.RootElement.TryGetProperty("startPublicationDate", out _));
        Assert.False(json.RootElement.TryGetProperty("endPublicationDate", out _));
    }

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHttpMessageHandler(handler));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request);
    }
}
