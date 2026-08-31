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
        Assert.Equal("Antineoplazice", Assert.Single(first.CpvNames ?? []));
        // The search payload carries the numeric ids needed to resolve the published documents.
        Assert.Equal(100159118, first.NoticeId);
        Assert.Equal(2, first.SysNoticeTypeId);
        // Petroșani is not a county seat → no county is derivable; the tender is excluded only when a radius filter is active.
        Assert.Null(first.County);
        Assert.Equal(new DateTimeOffset(2019, 4, 2, 1, 35, 1, TimeSpan.FromHours(3)), first.PublicationDate);
        Assert.Equal(new DateTimeOffset(2019, 5, 9, 15, 0, 0, TimeSpan.FromHours(3)), first.Deadline);

        SeapTender tender = SeapTenderMapper.MapNotice(first, DateTimeOffset.UtcNow);
        Assert.Equal("33652100-6", Assert.Single(tender.CpvCodes));
        Assert.Equal(14867462m, tender.EstimatedValue);
        Assert.Equal("RON", tender.Currency);
        Assert.Null(tender.Description);
        // The numeric notice id/type is persisted so the published documents can be resolved on demand.
        Assert.Equal(100159118, tender.NoticeId);
        Assert.Equal(2, tender.SysNoticeTypeId);
    }

    [Fact]
    public void CountyFallsBackToTheContractTitleWhenTheAuthorityNameHasNone()
    {
        // The platform's search payload has no structured county field: an authority whose name carries
        // no county (a school, a generic institution) resolves through the notice title ("județul Suceava").
        const string titleFallback = """
        {
          "total": 1,
          "searchTooLong": false,
          "items": [
            {
              "noticeNo": "SCN-SV-1",
              "contractTitle": "Reabilitare termică corp C, județul Suceava",
              "contractingAuthorityNameAndFN": "LICEUL TEHNOLOGIC ION SAVA",
              "cpvCodeAndName": "45211100-0 - Lucrari de constructii pentru locuinte",
              "estimatedValueRon": 1000,
              "noticeStateDate": "2026-08-31T10:00:00+03:00",
              "maxTenderReceiptDeadline": "2026-09-30T15:00:00+03:00",
              "noticeId": 1,
              "sysNoticeTypeId": 17
            }
          ]
        }
        """;
        SeapTenderNoticeDto fromTitle = Assert.Single(SeapNoticeParser.ParseNotices(titleFallback));
        Assert.Equal("Suceava", fromTitle.County);

        // The authority name wins over the title, so a cross-county worksite in the title can never
        // re-tag the contracting authority's own county.
        const string nameWins = """
        {
          "total": 1,
          "searchTooLong": false,
          "items": [
            {
              "noticeNo": "CN-CJ-1",
              "contractTitle": "Execuție drum, comuna Mălini, județul Suceava",
              "contractingAuthorityNameAndFN": "MUNICIPIUL CLUJ-NAPOCA",
              "cpvCodeAndName": "45233120-6 - Lucrari de constructii de drumuri",
              "estimatedValueRon": 1000,
              "noticeStateDate": "2026-08-31T10:00:00+03:00",
              "maxTenderReceiptDeadline": "2026-09-30T15:00:00+03:00",
              "noticeId": 2,
              "sysNoticeTypeId": 2
            }
          ]
        }
        """;
        SeapTenderNoticeDto fromName = Assert.Single(SeapNoticeParser.ParseNotices(nameWins));
        Assert.Equal("Cluj", fromName.County);
    }

    [Fact]
    public void MissingOptionalFieldsNormalizeToEmptyOrNull()
    {
        SeapTenderNoticeDto notice = Assert.Single(SeapNoticeParser.ParseNotices(Fixture("seap-notices-optional-missing.json")));

        Assert.Equal("CN1010447", notice.Id);
        Assert.Contains("reparatii", notice.Title);
        Assert.Contains("Apele Romane", notice.BuyerName);
        Assert.Empty(notice.CpvCodes);
        // The authority name carries no county, but the notice title does („S.G.A. Vrancea") — the
        // title-fallback derivation makes the tender reachable under its county filter (previously null).
        Assert.Equal("Vrancea", notice.County);
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
        Assert.Equal("Ilfov", notice.County);
        Assert.Null(notice.EstimatedValue);
        Assert.Equal(DateTimeOffset.MinValue, notice.Deadline);

        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Empty(tender.CpvCodes);
        Assert.Null(tender.EstimatedValue);
    }

    [Fact]
    public void CpvLabelAndCountyAreExtractedFromRealWireShape()
    {
        string json = """
            {"total":1,"items":[{"noticeNo":"CN-GARD-1","contractTitle":"Montare garduri","cpvCodeAndName":"45342000-6 - Montare de garduri (Rev.2)","contractingAuthorityNameAndFN":"17095927 - DIRECTIA GENERALA DE ASISTENTA SOCIALA SI PROTECTIA COPILULUI VASLUI","estimatedValueRon":1000,"noticeStateDate":"2019-04-02T01:35:01+03:00","maxTenderReceiptDeadline":"2019-05-09T15:00:00+03:00"}],"searchTooLong":false}
            """;

        SeapTenderNoticeDto notice = Assert.Single(SeapNoticeParser.ParseNotices(json));
        Assert.Equal("45342000-6", Assert.Single(notice.CpvCodes));
        Assert.Equal("Montare de garduri", Assert.Single(notice.CpvNames ?? []));
        Assert.Equal("Vaslui", notice.County);

        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Equal("Montare de garduri", Assert.Single(tender.CpvNames));
        Assert.Equal("Vaslui", tender.County);
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

    [Fact]
    public void PublishedDocumentsPayloadParsesIntoTheDocumentList()
    {
        const string payload = """
        {
          "isPubOrSu": true,
          "dfNoticeDocs": [
            { "noticeDocumentUrl": "api-pub/files/noticedoc/abc", "noticeDocumentName": "FisaDate.pdf", "noticeDocumentCode": "CN1048925/00009" },
            { "noticeDocumentUrl": "api-pub/files/noticedoc/def", "noticeDocumentName": "Caiet de sarcini.pdf.p7s", "noticeDocumentCode": "CN1048925/00005" },
            { "noticeDocumentUrl": "", "noticeDocumentName": "", "noticeDocumentCode": "" }
          ]
        }
        """;

        IReadOnlyCollection<SeapNoticeDocumentDto> documents = SeapNoticeParser.ParseNoticeDocuments(payload);

        Assert.Equal(2, documents.Count);
        Assert.Equal("CN1048925/00009", documents.ElementAt(0).Code);
        Assert.Equal("FisaDate.pdf", documents.ElementAt(0).FileName);
        Assert.Equal("api-pub/files/noticedoc/abc", documents.ElementAt(0).DownloadUrl);
        Assert.Equal("Caiet de sarcini.pdf.p7s", documents.ElementAt(1).FileName);
    }

    [Fact]
    public void PublishedDocumentsErrorAndGarbagePayloadsDegradeToEmpty()
    {
        // The real error shape old notices return (their files were purged).
        Assert.Empty(SeapNoticeParser.ParseNoticeDocuments(
            "[\"Eroare de sistem 260831-8E\",\"Object reference not set to an instance of an object.\"]"));
        Assert.Empty(SeapNoticeParser.ParseNoticeDocuments("{}"));
        Assert.Empty(SeapNoticeParser.ParseNoticeDocuments("nu e json"));
        Assert.Empty(SeapNoticeParser.ParseNoticeDocuments(""));
    }

    [Fact]
    public async Task ApiClientFetchesPublishedDocumentsAndResolvesAbsoluteUrls()
    {
        const string payload = """
        {
          "isPubOrSu": true,
          "dfNoticeDocs": [
            { "noticeDocumentUrl": "api-pub/files/noticedoc/abc", "noticeDocumentName": "FisaDate.pdf", "noticeDocumentCode": "CN1048925/00009" }
          ]
        }
        """;
        RecordingHandler handler = new(payload);
        using HttpClient httpClient = new(handler);
        StubHttpClientFactory factory = new(httpClient);
        IOptions<SeapOptions> options = Options.Create(new SeapOptions { BaseUrl = "https://e-licitatie.ro", RequestTimeoutSeconds = 60 });
        SeapApiClient client = new(factory, options, NullLogger<SeapApiClient>.Instance);

        IReadOnlyCollection<SeapNoticeDocumentDto> documents = await client.FetchNoticeDocumentsAsync(100159118, 2, CancellationToken.None);

        SeapNoticeDocumentDto document = Assert.Single(documents);
        Assert.Equal("FisaDate.pdf", document.FileName);
        // The relative noticeDocumentUrl is resolved against the configured base.
        Assert.Equal("https://e-licitatie.ro/api-pub/files/noticedoc/abc", document.DownloadUrl);
        Assert.NotNull(handler.LastRequest);
        Assert.Contains("GetDfNoticeSectionFiles", handler.LastRequest!.RequestUri!.AbsoluteUri);
        Assert.Contains("initNoticeId=100159118", handler.LastRequest.RequestUri.AbsoluteUri);
        Assert.Contains("sysNoticeTypeId=2", handler.LastRequest.RequestUri.AbsoluteUri);
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
