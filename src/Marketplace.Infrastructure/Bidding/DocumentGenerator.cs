using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Marketplace.Infrastructure.Bidding;

public sealed partial class DocumentGenerator : IDocumentGenerator
{
    public Task<GeneratedDocument> GenerateTechnicalProposalAsync(BiddingProposal proposal, CancellationToken cancellationToken)
    {
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
        return Task.FromResult(new GeneratedDocument(
            stream.ToArray(),
            fileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }

    public Task<GeneratedDocument> GenerateBillOfQuantitiesAsync(BiddingProposal proposal, CancellationToken cancellationToken)
    {
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
        return Task.FromResult(new GeneratedDocument(
            stream.ToArray(),
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }
}
