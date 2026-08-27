using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Marketplace.Infrastructure.Bidding;

public sealed partial class DocumentGenerator : IDocumentGenerator
{
    private readonly ILogger<DocumentGenerator> logger;

    public DocumentGenerator(ILogger<DocumentGenerator>? logger = null) => this.logger = logger ?? NullLogger<DocumentGenerator>.Instance;

    public Task<GeneratedDocument> GenerateTechnicalProposalAsync(BiddingProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Sections.Count == 0)
        {
            LogTechnicalProposalEmpty(logger);
            throw new InvalidOperationException("Propunerea tehnică nu poate fi generată: răspunsul LLM nu conține secțiuni.");
        }

        using MemoryStream stream = new();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body!;
            body.AppendChild(new Paragraph(new Run(new Text(proposal.Title)))
            {
                ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center })
            });
            foreach (ProposalSection section in proposal.Sections)
            {
                body.AppendChild(new Paragraph(new Run(new Text(section.Heading))));
                body.AppendChild(new Paragraph(new Run(new Text(section.Content))));
            }
            mainPart.Document.Save();
        }
        stream.Position = 0;
        string fileName = $"OfertaTehnica_{DateTime.UtcNow:yyyyMMddHHmm}.docx";
        byte[] content = stream.ToArray();
        LogTechnicalProposalGenerated(logger, content.Length, proposal.Sections.Count);
        return Task.FromResult(new GeneratedDocument(
            content,
            fileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    public Task<GeneratedDocument> GenerateBillOfQuantitiesAsync(BiddingProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.BillOfQuantities.Count == 0)
        {
            LogBillOfQuantitiesEmpty(logger);
            throw new InvalidOperationException("Lista de cantități nu poate fi generată: răspunsul LLM nu conține linii.");
        }

        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Lista de cantitati");
        sheet.Cell(1, 1).Value = "Nr. crt.";
        sheet.Cell(1, 2).Value = "Descriere";
        sheet.Cell(1, 3).Value = "Unitate";
        sheet.Cell(1, 4).Value = "Cantitate";
        sheet.Cell(1, 5).Value = "Pret unitar (lei)";
        sheet.Cell(1, 6).Value = "Total (lei)";
        int row = 2;
        foreach (BoqLine line in proposal.BillOfQuantities)
        {
            sheet.Cell(row, 1).Value = line.Position;
            sheet.Cell(row, 2).Value = line.Description;
            sheet.Cell(row, 3).Value = line.Unit;
            sheet.Cell(row, 4).Value = line.Quantity;
            sheet.Cell(row, 5).Value = line.UnitPrice;
            sheet.Cell(row, 6).Value = line.Total;
            row++;
        }
        sheet.Columns().AdjustToContents();
        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        stream.Position = 0;
        string fileName = $"Deviz_{DateTime.UtcNow:yyyyMMddHHmm}.xlsx";
        byte[] content = stream.ToArray();
        LogBillOfQuantitiesGenerated(logger, content.Length, proposal.BillOfQuantities.Count);
        return Task.FromResult(new GeneratedDocument(
            content,
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    [LoggerMessage(EventId = 5604, Level = LogLevel.Error, Message = "[ERROR] Nu se poate genera propunerea tehnică: răspunsul LLM nu conține secțiuni.")]
    private static partial void LogTechnicalProposalEmpty(ILogger logger);
    [LoggerMessage(EventId = 5605, Level = LogLevel.Information, Message = "[AutoBid] Generated technical proposal .docx: {Bytes} bytes ({SectionCount} sections).")]
    private static partial void LogTechnicalProposalGenerated(ILogger logger, int bytes, int sectionCount);
    [LoggerMessage(EventId = 5606, Level = LogLevel.Error, Message = "[ERROR] Nu se poate genera lista de cantități: răspunsul LLM nu conține linii.")]
    private static partial void LogBillOfQuantitiesEmpty(ILogger logger);
    [LoggerMessage(EventId = 5607, Level = LogLevel.Information, Message = "[AutoBid] Generated bill of quantities .xlsx: {Bytes} bytes ({BoqLineCount} lines).")]
    private static partial void LogBillOfQuantitiesGenerated(ILogger logger, int bytes, int boqLineCount);
}
