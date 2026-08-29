using Marketplace.Application.AutoBidding;
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
    [InlineData(0, 25)]
    [InlineData(19_999, 25)]
    [InlineData(20_000, 75)]
    [InlineData(99_999.99, 75)]
    [InlineData(100_000, 300)]
    [InlineData(250_000, 300)]
    [InlineData(500_000, 500)]
    [InlineData(999_999.99, 500)]
    [InlineData(1_000_000, 1_000)]
    [InlineData(4_999_999, 1_000)]
    [InlineData(5_000_000, 2_000)]
    [InlineData(9_999_999, 2_000)]
    [InlineData(10_000_000, 3_000)]
    [InlineData(10_000_001, 3_000)]
    [InlineData(15_000_000, 4_000)]
    [InlineData(20_000_000, 5_000)]
    [InlineData(25_000_000, 6_000)]
    [InlineData(30_000_000, 7_000)]
    [InlineData(35_000_000, 8_000)]
    [InlineData(40_000_000, 9_000)]
    [InlineData(45_000_000, 10_000)]
    [InlineData(49_999_999, 10_000)]
    [InlineData(50_000_000, 20_000)]
    [InlineData(100_000_000, 20_000)]
    public void PricingTiersFollowEstimatedValue(decimal valueEur, int expectedEur) =>
        Assert.Equal(expectedEur, PricingCalculator.ComputePriceEur(valueEur));

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
    public void ParserRejectsIncompleteLlmOutput(string json)
    {
        // Schema-level failures are user-facing pipeline errors (422), not internal 500s.
        AutoBiddingPipelineException exception = Assert.Throws<AutoBiddingPipelineException>(() => BiddingJsonParser.Parse(json));
        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public void ParserRejectsBoqLineWithoutQuantity()
    {
        AutoBiddingPipelineException exception = Assert.Throws<AutoBiddingPipelineException>(() => BiddingJsonParser.Parse(
            """
            {"title":"Ofertă","technicalProposal":{"sections":[{"heading":"H","content":"C"}]},
             "billOfQuantities":[{"position":1,"description":"Ciment","unit":"buc","unitPrice":120,"total":120}]}
            """));
        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public void ParserRejectsBoqLineWithoutDescription()
    {
        AutoBiddingPipelineException exception = Assert.Throws<AutoBiddingPipelineException>(() => BiddingJsonParser.Parse(
            """
            {"title":"Ofertă","technicalProposal":{"sections":[{"heading":"H","content":"C"}]},
             "billOfQuantities":[{"position":1,"description":"","unit":"buc","quantity":2,"unitPrice":120,"total":240}]}
            """));
        Assert.Equal(422, exception.StatusCode);
    }

    [Fact]
    public void ParserStripsMarkdownCodeFences()
    {
        string fenced =
            "```json\n" +
            StaticMockLlmProvider.RomanianProcurementExpertProposal.Trim() +
            "\n```";
        BiddingProposal proposal = BiddingJsonParser.Parse(fenced);
        Assert.Equal("Ofertă tehnică — Lucrări de construcții", proposal.Title);
        Assert.NotEmpty(proposal.Sections);
        Assert.Single(proposal.BillOfQuantities);
    }

    [Fact]
    public void ParserReadsFinancialSummaryRecap()
    {
        BiddingProposal proposal = BiddingJsonParser.Parse(
            """
            {"title":"Ofertă","technicalProposal":{"sections":[{"heading":"H","content":"C"}]},
             "billOfQuantities":[{"position":1,"description":"Ciment","unit":"buc","quantity":2,"unitPrice":120,"total":240}],
             "financialSummary":[
               {"label":"TOTAL GENERAL (fără TVA)","value":1500000,"note":null},
               {"label":"TVA (21.00%)","value":315000,"note":null},
               {"label":"TOTAL GENERAL (inclusiv TVA)","value":1815000,"note":"T4 = T3 + beneficiu"}
             ]}
            """);
        Assert.Equal(3, proposal.FinancialSummary.Count);
        Assert.Equal("TVA (21.00%)", proposal.FinancialSummary.ElementAt(1).Label);
        Assert.Equal(315000m, proposal.FinancialSummary.ElementAt(1).Value);
        Assert.Equal("T4 = T3 + beneficiu", proposal.FinancialSummary.ElementAt(2).Note);
    }

    [Fact]
    public void ClaudeResponseExtractsTextBlockAfterThinkingBlock()
    {
        const string body =
            """{"content":[{"type":"thinking","thinking":"plan","signature":"sig"},{"type":"text","text":"{\"title\":\"OK\"}"}],"model":"claude-sonnet-5"}""";
        Assert.Equal("{\"title\":\"OK\"}", ClaudeResponse.ExtractText(body));
    }

    [Fact]
    public void ClaudeResponseThrowsWhenNoTextBlock()
    {
        // A response without any text block is a user-facing model failure (502), not an internal 500.
        AutoBiddingPipelineException exception = Assert.Throws<AutoBiddingPipelineException>(() =>
            ClaudeResponse.ExtractText("""{"content":[{"type":"thinking","thinking":"plan"}]}"""));
        Assert.Equal(502, exception.StatusCode);
    }

    [Fact]
    public async Task DocumentGeneratorFormatsProposalForHumanReadability()
    {
        BiddingProposal proposal = new(
            "Ofertă tehnică — Lucrări de construcții",
            [
                new ProposalSection("1. Prezentare generală", "Firma își asumă toate cerințele din caietul de sarcini."),
                new ProposalSection("2. Descrierea lucrărilor", "2.1. Prima lucrare conform proiectului.\n\n2.2. A doua lucrare, cu materiale certificate.\n\n2.3. A treia lucrare.")
            ],
            [new BoqLine(1, "Ciment", "buc", 2, 250m, 500m)],
            [new FinancialSummaryLine("TOTAL GENERAL (fără TVA)", 1500000m, null),
             new FinancialSummaryLine("TVA (21.00%)", 315000m, null),
             new FinancialSummaryLine("TOTAL GENERAL (inclusiv TVA)", 1815000m, "T4 = T3 + beneficiu")]);

        DocumentGenerator generator = new();

        GeneratedDocument docx = await generator.GenerateTechnicalProposalAsync(proposal, CancellationToken.None);
        using var word = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(docx.Content), false);
        string xml = word.MainDocumentPart!.Document.OuterXml;
        // Forest Green headings + 18pt bold title + footer page field + A4 section properties.
        Assert.Contains("w:color w:val=\"1E3D2F\"", xml, StringComparison.Ordinal);
        Assert.Contains("<w:sz w:val=\"36\"", xml, StringComparison.Ordinal);
        Assert.NotEmpty(word.MainDocumentPart.FooterParts);
        Assert.Contains("w:pgSz w:w=\"11906\"", xml, StringComparison.Ordinal);
        // Multi-block content is split into separate paragraphs: heading (1) + 3 body paragraphs + title/meta/divider.
        int paragraphs = System.Text.RegularExpressions.Regex.Count(xml, "<w:p>");
        Assert.True(paragraphs >= 7, $"expected separate paragraphs, got {paragraphs}");

        GeneratedDocument xlsx = await generator.GenerateBillOfQuantitiesAsync(proposal, CancellationToken.None);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx.Content));
        ClosedXML.Excel.IXLWorksheet sheet = workbook.Worksheet(1);
        int lastRow = sheet.LastRowUsed()!.RowNumber();
        Assert.True(sheet.Cell(1, 1).Style.Font.Bold, "header must be bold");
        // The TOTAL row carries the SUM formula; the financial recap is rendered below it.
        int totalRow = -1;
        for (int r = 2; r <= lastRow; r++)
        {
            if (sheet.Cell(r, 2).GetString().Trim() == "TOTAL") { totalRow = r; break; }
        }
        Assert.True(totalRow > 0, "TOTAL row not found");
        Assert.StartsWith("SUM(F", sheet.Cell(totalRow, 6).FormulaA1, StringComparison.OrdinalIgnoreCase);
        string sheetText = string.Join(" ", sheet.RowsUsed().Select(row => row.Cell(2).GetString()));
        Assert.Contains("Recapitulație financiară", sheetText, StringComparison.Ordinal);
        Assert.Contains("TOTAL GENERAL (fără TVA)", sheetText, StringComparison.Ordinal);
        Assert.Contains("TVA (21.00%)", sheetText, StringComparison.Ordinal);
    }

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
