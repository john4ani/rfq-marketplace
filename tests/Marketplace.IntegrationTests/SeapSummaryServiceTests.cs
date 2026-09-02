using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Seap;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marketplace.IntegrationTests;

// SeapSummaryService: generates the „Sinteză AI" bullets and decision-critical detail fields from the notice
// data the importer holds (title, CPV, estimated value, buyer, county, deadlines, description) plus optional
// attachment text. Guards the JSON contract (exact schema, Markdown-wrapped output tolerated, garbage/throwing
// LLM degrade to null — the tender still imports without AI details).
public sealed class SeapSummaryServiceTests
{
    private static SeapTender Tender() => SeapTender.Import(
        "SCN-test-123", "Reabilitare clădire școlară", null, ["45214200-1"], ["Lucrari de constructii de gradinite"],
        2_420_466m, "RON", "Primaria Comunei Poiana Dambovita", "Dâmbovița",
        new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 30, 15, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow);

    private static SeapSummaryService Service(ILlmProvider llm) =>
        new(llm, NullLogger<SeapSummaryService>.Instance);

    [Fact]
    public async Task ValidJsonOutputIsParsedAndNormalized()
    {
        const string json = """
        {
          "summaryPoints": ["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"],
          "description": "Reabilitarea clădirii școlii din Chiajna.",
          "awardCriterion": "Cel mai bun raport calitate-preț (60% preț, 40% tehnic)",
          "participationGuarantee": "120.000 RON",
          "fundingSource": "PNRR",
          "contractDuration": "12 luni",
          "qualificationRequirements": "Minim 2 contracte de reabilitare clădiri în ultimii 3 ani",
          "keyPersonnel": "Manager de proiect, Inginer RTE",
          "certificates": "ISO 9001, ISO 45001"
        }
        """;

        SeapAiExtraction? extraction = await Service(new StaticMockLlmProvider(json))
            .GenerateAsync(Tender(), attachmentText: null, CancellationToken.None);

        Assert.NotNull(extraction);
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], extraction!.SummaryPoints);
        Assert.Equal("Reabilitarea clădirii școlii din Chiajna.", extraction.Description);
        Assert.Equal("Cel mai bun raport calitate-preț (60% preț, 40% tehnic)", extraction.AwardCriterion);
        Assert.Equal("120.000 RON", extraction.ParticipationGuarantee);
        Assert.Equal("PNRR", extraction.FundingSource);
        Assert.Equal("12 luni", extraction.ContractDuration);
        Assert.Equal("Minim 2 contracte de reabilitare clădiri în ultimii 3 ani", extraction.QualificationRequirements);
        Assert.Equal("Manager de proiect, Inginer RTE", extraction.KeyPersonnel);
        Assert.Equal("ISO 9001, ISO 45001", extraction.Certificates);
    }

    [Fact]
    public async Task MarkdownWrappedJsonIsTolerated()
    {
        const string json = "```json\n{\"summaryPoints\": [\"Garanție: 50.000 RON\"], \"awardCriterion\": \"Prețul cel mai scăzut\"}\n```";

        SeapAiExtraction? extraction = await Service(new StaticMockLlmProvider(json))
            .GenerateAsync(Tender(), attachmentText: null, CancellationToken.None);

        Assert.NotNull(extraction);
        Assert.Equal(["Garanție: 50.000 RON"], extraction!.SummaryPoints);
        Assert.Equal("Prețul cel mai scăzut", extraction.AwardCriterion);
    }

    [Fact]
    public async Task FieldsOnlyOutputWithoutBulletsStillYieldsAnExtraction()
    {
        const string json = """{"awardCriterion":"Prețul cel mai scăzut"}""";

        SeapAiExtraction? extraction = await Service(new StaticMockLlmProvider(json))
            .GenerateAsync(Tender(), attachmentText: null, CancellationToken.None);

        Assert.NotNull(extraction);
        Assert.Equal("Prețul cel mai scăzut", extraction!.AwardCriterion);
        Assert.Empty(extraction.SummaryPoints);
    }

    [Fact]
    public async Task GarbageAndEmptyOutputsDegradeToNull()
    {
        SeapSummaryService service = Service(new StaticMockLlmProvider("nu sunt JSON"));
        Assert.Null(await service.GenerateAsync(Tender(), attachmentText: null, CancellationToken.None));

        SeapSummaryService empty = Service(new StaticMockLlmProvider("{}"));
        Assert.Null(await empty.GenerateAsync(Tender(), attachmentText: null, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowingLlmDegradesToNull()
    {
        SeapSummaryService service = Service(new ThrowingLlmProvider());
        Assert.Null(await service.GenerateAsync(Tender(), attachmentText: null, CancellationToken.None));
    }

    [Fact]
    public async Task BulletsAreCappedAtFiveAndTruncatedToOneHundredFortyCharacters()
    {
        string longBullet = new('a', 200);
        string[] bullets = [longBullet, "un", "doi", "trei", "patru", "cinci"];
        string json = $$"""{"summaryPoints": [{{string.Join(", ", bullets.Select(b => $"\"{b}\""))}}]}""";

        SeapAiExtraction? extraction = await Service(new StaticMockLlmProvider(json))
            .GenerateAsync(Tender(), attachmentText: null, CancellationToken.None);

        Assert.NotNull(extraction);
        Assert.Equal(5, extraction!.SummaryPoints.Count);
        Assert.Equal(141, extraction.SummaryPoints.First().Length); // 140 chars + trailing ellipsis
    }

    [Fact]
    public async Task PromptCarriesTheTenderDataAndTheOptionalAttachmentText()
    {
        RecordingLlmProvider llm = new("""{"summaryPoints": ["Garanție: 50.000 RON"]}""");
        string attachmentText = "Caiet de sarcini: garanție de participare 50.000 RON, termen execuție 12 luni.";

        await Service(llm).GenerateAsync(Tender(), attachmentText, CancellationToken.None);

        Assert.Contains("Reabilitare clădire școlară", llm.UserPrompt);
        Assert.Contains("45214200-1", llm.UserPrompt);
        Assert.Contains("2.420.466", llm.UserPrompt);
        Assert.Contains("Primaria Comunei Poiana Dambovita", llm.UserPrompt);
        Assert.Contains("Dâmbovița", llm.UserPrompt);
        Assert.Contains("30.09.2026", llm.UserPrompt);
        Assert.Contains("EXTRAS DIN FIȘA DE DATE / CAIETUL DE SARCINI", llm.UserPrompt);
        Assert.Contains("garanție de participare 50.000 RON", llm.UserPrompt);
        Assert.Contains("\"summaryPoints\"", llm.SystemPrompt);
        Assert.Contains("nu inventa date", llm.SystemPrompt);
    }

    private sealed class ThrowingLlmProvider : ILlmProvider
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("LLM indisponibil.");
    }

    private sealed class RecordingLlmProvider(string jsonResponse) : ILlmProvider
    {
        public string SystemPrompt { get; private set; } = string.Empty;
        public string UserPrompt { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
            return Task.FromResult(jsonResponse);
        }
    }
}
