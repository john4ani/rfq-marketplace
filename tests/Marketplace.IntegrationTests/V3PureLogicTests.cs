using Marketplace.Application.Billing;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Seap;

namespace Marketplace.IntegrationTests;

// Pure business logic + mocked-LLM document extraction, per the V3 testing addendum.
public sealed class V3PureLogicTests
{
    [Theory]
    [InlineData(0, 29)]
    [InlineData(50_000, 29)]
    [InlineData(99_999.99, 29)]
    [InlineData(100_000, 49)]
    [InlineData(250_000, 49)]
    [InlineData(500_000, 49)]
    [InlineData(500_001, 99)]
    [InlineData(2_000_000, 99)]
    public void PricingTiersFollowEstimatedValue(decimal value, int expectedEur) =>
        Assert.Equal(expectedEur, PricingCalculator.ComputePriceEur(value));

    [Fact]
    public void ChunkerSplitsLongTextWithOverlapAndPreservesOrder()
    {
        string text = string.Join(" ", Enumerable.Range(1, 200).Select(index => $"cuvânt{index}"));
        IReadOnlyCollection<string> chunks = TextChunker.Chunk(text, chunkSize: 400, overlap: 100);
        Assert.True(chunks.Count > 1);
        Assert.Equal("cuvânt1", chunks.First().Split(' ')[0]);
        Assert.Contains("cuvânt200", chunks.Last());
    }

    [Fact]
    public void ChunkerReturnsSingleChunkForShortText() =>
        Assert.Single(TextChunker.Chunk("Un text scurt."));

    [Fact]
    public void ParserHandlesStaticMockProposalOutput()
    {
        BiddingProposal proposal = BiddingJsonParser.Parse(StaticMockLlmProvider.RomanianProcurementExpertProposal);
        Assert.Equal("Ofertă tehnică — Lucrări de construcții", proposal.Title);
        Assert.NotEmpty(proposal.Sections);
        Assert.Single(proposal.BillOfQuantities);
        Assert.Equal(500m, proposal.BillOfQuantities.Single().Total);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":\"Doar titlu\"}")]
    [InlineData("{\"title\":\"Fără deviz\",\"technicalProposal\":{\"sections\":[{\"heading\":\"H\",\"content\":\"C\"}]}}")]
    public void ParserRejectsIncompleteLlmOutput(string json) =>
        Assert.Throws<InvalidOperationException>(() => BiddingJsonParser.Parse(json));

    [Fact]
    public void ParserRejectsBoqLineWithoutQuantity() =>
        Assert.Throws<InvalidOperationException>(() => BiddingJsonParser.Parse(
            """
            {"title":"Ofertă","technicalProposal":{"sections":[{"heading":"H","content":"C"}]},
             "billOfQuantities":[{"position":1,"description":"Ciment","unit":"buc","unitPrice":120,"total":120}]}
            """));

    [Fact]
    public void ParserRejectsBoqLineWithoutDescription() =>
        Assert.Throws<InvalidOperationException>(() => BiddingJsonParser.Parse(
            """
            {"title":"Ofertă","technicalProposal":{"sections":[{"heading":"H","content":"C"}]},
             "billOfQuantities":[{"position":1,"description":"","unit":"buc","quantity":2,"unitPrice":120,"total":240}]}
            """));

    [Fact]
    public void ClaudeResponseExtractsTextBlockAfterThinkingBlock()
    {
        const string body =
            """{"content":[{"type":"thinking","thinking":"plan","signature":"sig"},{"type":"text","text":"{\"title\":\"OK\"}"}],"model":"claude-sonnet-5"}""";
        Assert.Equal("{\"title\":\"OK\"}", ClaudeResponse.ExtractText(body));
    }

    [Fact]
    public void ClaudeResponseThrowsWhenNoTextBlock() =>
        Assert.Throws<InvalidOperationException>(() =>
            ClaudeResponse.ExtractText("""{"content":[{"type":"thinking","thinking":"plan"}]}"""));

    [Fact]
    public async Task DocumentGeneratorProducesOpenableDocxAndXlsxFromParsedProposal()
    {
        BiddingProposal proposal = BiddingJsonParser.Parse(StaticMockLlmProvider.RomanianProcurementExpertProposal);
        DocumentGenerator generator = new();
        GeneratedDocument docx = await generator.GenerateTechnicalProposalAsync(proposal, CancellationToken.None);
        GeneratedDocument xlsx = await generator.GenerateBillOfQuantitiesAsync(proposal, CancellationToken.None);
        Assert.True(docx.Content.Length > 1000);
        Assert.True(xlsx.Content.Length > 1000);
        Assert.EndsWith(".docx", docx.FileName);
        Assert.EndsWith(".xlsx", xlsx.FileName);

        using (var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx.Content), false))
            Assert.NotNull(word.MainDocumentPart?.Document);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx.Content));
        Assert.True(workbook.Worksheets.Count >= 1);
    }

    [Fact]
    public void SeapTenderMapperMapsNoticeToAggregate()
    {
        SeapTenderNoticeDto notice = new("SEAP-123", "Lucrări de reabilitare", "Descriere", ["45200000"],
            250_000m, "RON", "Primăria Municipiului Iași", DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(20), [new("caiet.zip", "https://e-licitatie.ro/dl/1")]);
        SeapTender tender = SeapTenderMapper.MapNotice(notice, DateTimeOffset.UtcNow);
        Assert.Equal("SEAP-123", tender.SeapExternalId);
        Assert.Equal(250_000m, tender.EstimatedValue);
        Assert.Equal("Primăria Municipiului Iași", tender.BuyerName);
        Assert.Equal(SeapTenderStatus.Imported, tender.Status);
    }

    [Theory]
    [InlineData("prod", "prod/")]
    [InlineData("staging", "staging/")]
    [InlineData("/prod/", "prod/")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData(" prod/ ", "prod/")]
    public void S3RootPrefixIsNormalized(string? configured, string expected)
    {
        S3Options options = new() { RootPrefix = configured ?? string.Empty };
        Assert.Equal(expected, options.NormalizedRootPrefix);
    }

    [Theory]
    [InlineData("prod", "prod/seap/123/abc.zip")]
    [InlineData("staging", "staging/seap/123/abc.zip")]
    [InlineData("", "seap/123/abc.zip")]
    public void S3RootPrefixAppliesToObjectKeys(string prefix, string expectedKey)
    {
        S3Options options = new() { RootPrefix = prefix };
        string key = $"{options.NormalizedRootPrefix}seap/123/abc.zip";
        Assert.Equal(expectedKey, key);
    }
}
