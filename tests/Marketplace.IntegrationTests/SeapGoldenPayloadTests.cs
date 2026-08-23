using System.Net;
using System.Text;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Seap;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Golden-payload regression suite for the Anunțuri SEAP notice parser. The fixtures under tests/Fixtures
// are real e-licitatie.ro responses: seap-notices-full.json is a verbatim capture of the public
// api-pub/NoticeCommon/GetCNoticeList search; the three JSON variants are derived from real items by
// removing optional keys; seap-notices-html-error.html is the real SPA page. Together they prove that
// optional fields are tolerated without NullReferenceException and that non-JSON payloads degrade safely.
public sealed class SeapGoldenPayloadTests
{
    [Theory]
    [InlineData("seap-notices-full.json")]
    [InlineData("seap-notices-optional-missing.json")]
    [InlineData("seap-notices-no-cpv.json")]
    [InlineData("seap-notices-null-collections.json")]
    public void GoldenJsonPayloadsParseAndMapWithoutNullReference(string fixture)
    {
        IReadOnlyCollection<SeapTenderNoticeDto> notices = SeapNoticeParser.ParseNotices(Fixture(fixture));

        Assert.NotEmpty(notices);
        foreach (SeapTenderNoticeDto notice in notices)
        {
            SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
            Assert.NotNull(tender);
            Assert.NotNull(tender.CpvCodes);
            Assert.NotNull(tender.Attachments);
            Assert.Equal(SeapTenderStatus.Imported, tender.Status);
        }
    }

    [Fact]
    public void FullRealPayloadMapsEveryField()
    {
        IReadOnlyCollection<SeapTenderNoticeDto> notices = SeapNoticeParser.ParseNotices(Fixture("seap-notices-full.json"));

        SeapTenderNoticeDto first = notices.First();
        Assert.Equal("CN1010402", first.Id);
        Assert.Contains("MEDICAMENTE", first.Title);
        Assert.Contains("SPITALUL DE URGENTA PETROSANI", first.BuyerName);
        Assert.Equal("33652100-6", Assert.Single(first.CpvCodes));
        Assert.Equal(14867462m, first.EstimatedValue);
        Assert.Equal("RON", first.Currency);
        Assert.Null(first.Description);
        Assert.Empty(first.Attachments);
        Assert.Equal(new DateTimeOffset(2019, 4, 2, 1, 35, 1, TimeSpan.FromHours(3)), first.PublicationDate);
        Assert.Equal(new DateTimeOffset(2019, 5, 9, 15, 0, 0, TimeSpan.FromHours(3)), first.Deadline);

        SeapTender tender = SeapTenderMapper.MapNotice(first, DateTimeOffset.UtcNow);
        Assert.Equal("33652100-6", Assert.Single(tender.CpvCodes));
        Assert.Equal(14867462m, tender.EstimatedValue);
        Assert.Equal("RON", tender.Currency);
        Assert.Null(tender.Description);
    }

    [Fact]
    public void MissingOptionalFieldsNormalizeToEmptyOrNull()
    {
        SeapTenderNoticeDto notice = Assert.Single(SeapNoticeParser.ParseNotices(Fixture("seap-notices-optional-missing.json")));

        Assert.Equal("CN1010447", notice.Id);
        Assert.Contains("reparatii", notice.Title);
        Assert.Contains("Apele Romane", notice.BuyerName);
        Assert.Empty(notice.CpvCodes);
        Assert.Null(notice.EstimatedValue);
        Assert.Equal("RON", notice.Currency);
        Assert.Equal(DateTimeOffset.MinValue, notice.Deadline);

        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Empty(tender.CpvCodes);
        Assert.Null(tender.EstimatedValue);
    }

    [Fact]
    public void AbsentCpvKeyDoesNotThrowNullReference()
    {
        SeapTenderNoticeDto notice = Assert.Single(SeapNoticeParser.ParseNotices(Fixture("seap-notices-no-cpv.json")));

        Assert.Equal("CN1010518", notice.Id);
        Assert.Contains("MIXTURI ASFALTICE", notice.Title);
        Assert.Empty(notice.CpvCodes);
        Assert.Equal(7618240m, notice.EstimatedValue);

        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Empty(tender.CpvCodes);
        Assert.Equal(7618240m, tender.EstimatedValue);
    }

    [Fact]
    public void ExplicitNullOptionalFieldsAreTolerated()
    {
        SeapTenderNoticeDto notice = Assert.Single(SeapNoticeParser.ParseNotices(Fixture("seap-notices-null-collections.json")));

        Assert.Equal("CN1010558", notice.Id);
        Assert.Contains("cascaval", notice.Title);
        Assert.Contains("ECOVOL ILFOV", notice.BuyerName);
        Assert.Empty(notice.CpvCodes);
        Assert.Null(notice.EstimatedValue);
        Assert.Equal(DateTimeOffset.MinValue, notice.Deadline);

        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Empty(tender.CpvCodes);
        Assert.Null(tender.EstimatedValue);
    }

    [Fact]
    public void HtmlErrorPayloadDegradesToEmptyResultWithoutException()
    {
        Assert.Empty(SeapNoticeParser.ParseNotices(Fixture("seap-notices-html-error.html")));
    }

    [Fact]
    public void EmptyAndMalformedPayloadsReturnEmptyResult()
    {
        Assert.Empty(SeapNoticeParser.ParseNotices(string.Empty));
        Assert.Empty(SeapNoticeParser.ParseNotices("   "));
        Assert.Empty(SeapNoticeParser.ParseNotices("null"));
        // Real SEAP 400 error body: a JSON array, not the search envelope.
        Assert.Empty(SeapNoticeParser.ParseNotices("[\"Eroare de sistem 260823-19\",\"Object reference not set to an instance of an object.\"]"));
    }

    [Fact]
    public async Task ApiClientPostsRealSearchFilterAndParsesGoldenPayload()
    {
        string json = Fixture("seap-notices-full.json");
        RecordingHandler handler = new(json);
        using HttpClient httpClient = new(handler);
        StubHttpClientFactory factory = new(httpClient);
        IOptions<SeapOptions> options = Options.Create(new SeapOptions
        {
            BaseUrl = "https://e-licitatie.ro",
            NoticesPath = "api-pub/NoticeCommon/GetCNoticeList/",
            NoticeTypeIds = [2],
            NoticesPageSize = 20
        });
        SeapApiClient client = new(factory, options, NullLogger<SeapApiClient>.Instance);

        IReadOnlyCollection<SeapTenderNoticeDto> notices = await client.FetchRecentNoticesAsync(CancellationToken.None);

        HttpRequestMessage? request = handler.LastRequest;
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.EndsWith("api-pub/NoticeCommon/GetCNoticeList/", request.RequestUri!.AbsoluteUri);
        string body = handler.LastRequestBody ?? string.Empty;
        Assert.Contains("\"sysNoticeTypeIds\":[2]", body);
        Assert.Contains("\"pageSize\":20", body);

        SeapTenderNoticeDto first = notices.First();
        Assert.Equal("CN1010402", first.Id);
        Assert.Equal("33652100-6", Assert.Single(first.CpvCodes));
    }

    private static string Fixture(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));

    private sealed class RecordingHandler(string content) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
