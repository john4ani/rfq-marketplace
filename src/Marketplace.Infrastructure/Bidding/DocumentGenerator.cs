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

    // Forest Warm palette — the official MVP visual direction.
    private const string ForestGreen = "1E3D2F";
    private const string MutedGray = "6B7280";
    private const string FontFamily = "Calibri";
    private static readonly System.Text.RegularExpressions.Regex SubItemPattern =
        new(@"^(\d+(?:\.\d+)+\.?)\s*(.*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public Task<GeneratedDocument> GenerateTechnicalProposalAsync(BiddingProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Sections.Count == 0)
        {
            LogTechnicalProposalEmpty(logger);
            throw new InvalidOperationException("Propunerea tehnică nu poate fi generată: răspunsul LLM nu conține secțiuni.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using MemoryStream stream = new();
        using (WordprocessingDocument document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            Body body = mainPart.Document.Body!;

            // Header: title + meta line + divider.
            body.AppendChild(TitleParagraph(proposal.Title));
            body.AppendChild(MetaParagraph(now));
            body.AppendChild(DividerParagraph());

            // Sections: heading + body. Multi-paragraph content is split on blank lines so numbered
            // sub-items (e.g. "2.1. …") render as separate readable paragraphs.
            foreach (ProposalSection section in proposal.Sections)
            {
                body.AppendChild(SectionHeading(section.Heading));
                foreach (string block in SplitBodyBlocks(section.Content))
                    body.AppendChild(BodyParagraph(block));
            }

            // Footer with the page number, then the section properties that carry it.
            FooterPart footerPart = mainPart.AddNewPart<FooterPart>();
            footerPart.Footer = new Footer(FooterParagraph());
            string footerId = mainPart.GetIdOfPart(footerPart);
            body.AppendChild(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 }, // A4
                new PageMargin { Top = 1134, Right = 1134, Bottom = 1134, Left = 1134, Header = 708, Footer = 708 },
                new FooterReference { Type = HeaderFooterValues.Default, Id = footerId }));

            mainPart.Document.Save();
        }
        stream.Position = 0;
        string fileName = $"OfertaTehnica_{now:yyyyMMddHHmm}.docx";
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
        const string headerColor = "1E3D2F";
        const string totalColor = "F2E9D8";

        sheet.Cell(1, 1).Value = "Nr. crt.";
        sheet.Cell(1, 2).Value = "Descriere";
        sheet.Cell(1, 3).Value = "Unitate";
        sheet.Cell(1, 4).Value = "Cantitate";
        sheet.Cell(1, 5).Value = "Preț unitar (lei)";
        sheet.Cell(1, 6).Value = "Total (lei)";
        sheet.Range(1, 1, 1, 6).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 6).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(headerColor);
        sheet.Range(1, 1, 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = 2;
        foreach (BoqLine line in proposal.BillOfQuantities)
        {
            sheet.Cell(row, 1).Value = line.Position;
            sheet.Cell(row, 2).Value = line.Description;
            sheet.Cell(row, 3).Value = line.Unit;
            sheet.Cell(row, 4).Value = line.Quantity;
            sheet.Cell(row, 5).Value = line.UnitPrice;
            sheet.Cell(row, 6).Value = line.Total;
            sheet.Range(row, 1, row, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        // TOTAL row: bold, shaded, medium top border, summing the Total column.
        int totalRow = row;
        sheet.Cell(totalRow, 2).Value = "TOTAL";
        sheet.Cell(totalRow, 6).FormulaA1 = $"=SUM(F2:F{totalRow - 1})";
        sheet.Range(totalRow, 1, totalRow, 6).Style.Font.Bold = true;
        sheet.Range(totalRow, 1, totalRow, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(totalColor);
        sheet.Range(totalRow, 1, totalRow, 6).Style.Border.TopBorder = XLBorderStyleValues.Medium;
        sheet.Range(totalRow, 1, totalRow, 6).Style.Border.TopBorderColor = XLColor.FromHtml(headerColor);
        sheet.Cell(totalRow, 6).Style.NumberFormat.Format = "#,##0.00";

        sheet.SheetView.FreezeRows(1);
        sheet.Column(1).Width = 9;
        sheet.Column(2).Width = 70;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 14;
        sheet.Column(5).Width = 16;
        sheet.Column(6).Width = 16;

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

    private static Paragraph TitleParagraph(string text) =>
        Paragraph(Run(text, "36", bold: true, color: ForestGreen), JustificationValues.Center, before: 0, after: 120, keepNext: true);

    private static Paragraph MetaParagraph(DateTimeOffset now) =>
        Paragraph(Run($"Propunere tehnică · generată de CompaSpot pe {now:dd.MM.yyyy}", "18", italic: true, color: MutedGray),
            JustificationValues.Center, before: 0, after: 240);

    private static Paragraph DividerParagraph()
    {
        ParagraphBorders borders = new(new BottomBorder { Val = BorderValues.Single, Size = 6, Color = ForestGreen });
        return Paragraph(Run(string.Empty, "2"), JustificationValues.Left, before: 0, after: 120, borders: borders);
    }

    private static Paragraph SectionHeading(string text) =>
        Paragraph(Run(text, "26", bold: true, color: ForestGreen), JustificationValues.Left, before: 360, after: 160, keepNext: true);

    private static Paragraph BodyParagraph(string text)
    {
        System.Text.RegularExpressions.Match match = SubItemPattern.Match(text);
        if (match.Success)
        {
            // Bold the leading "2.1." token and indent the sub-item slightly.
            Paragraph paragraph = new(Run(match.Groups[1].Value + " ", "22", bold: true), Run(match.Groups[2].Value, "22"));
            paragraph.ParagraphProperties = ParagraphProperties(JustificationValues.Both, 0, 160, 276, firstLineIndent: 240);
            return paragraph;
        }
        return Paragraph(Run(text, "22"), JustificationValues.Both, before: 0, after: 160);
    }

    private static Paragraph FooterParagraph()
    {
        Paragraph paragraph = new(
            Run("CompaSpot · propunere tehnică · pagina ", "16", color: MutedGray),
            new Run(
                new FieldChar { FieldCharType = FieldCharValues.Begin },
                new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve },
                new FieldChar { FieldCharType = FieldCharValues.Separate },
                new Text("1"),
                new FieldChar { FieldCharType = FieldCharValues.End })
            {
                RunProperties = new RunProperties(new FontSize { Val = "16" }, new Color { Val = MutedGray })
            });
        paragraph.ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Right });
        return paragraph;
    }

    private static Paragraph Paragraph(
        Run run, JustificationValues alignment, int before, int after, bool keepNext = false,
        int? firstLineIndent = null, int lineSpacing = 276, ParagraphBorders? borders = null)
    {
        ParagraphProperties properties = ParagraphProperties(alignment, before, after, lineSpacing, firstLineIndent, borders);
        if (keepNext) properties.Append(new KeepNext());
        return new Paragraph(run) { ParagraphProperties = properties };
    }

    private static ParagraphProperties ParagraphProperties(
        JustificationValues alignment, int before, int after, int lineSpacing, int? firstLineIndent = null, ParagraphBorders? borders = null)
    {
        ParagraphProperties properties = new(
            new SpacingBetweenLines
            {
                Before = before.ToString(System.Globalization.CultureInfo.InvariantCulture),
                After = after.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Line = lineSpacing.ToString(System.Globalization.CultureInfo.InvariantCulture),
                LineRule = LineSpacingRuleValues.Auto
            },
            new Justification { Val = alignment });
        if (firstLineIndent is int indent)
            properties.Append(new Indentation { FirstLine = indent.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        if (borders is not null) properties.Append(borders);
        return properties;
    }

    private static Run Run(string text, string fontSizeHalfPoints, bool bold = false, bool italic = false, string? color = null)
    {
        RunProperties properties = new(
            new FontSize { Val = fontSizeHalfPoints },
            new RunFonts { Ascii = FontFamily, HighAnsi = FontFamily });
        if (bold) properties.Append(new Bold());
        if (italic) properties.Append(new Italic());
        if (color is not null) properties.Append(new Color { Val = color });
        return new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }) { RunProperties = properties };
    }

    private static IEnumerable<string> SplitBodyBlocks(string content)
    {
        foreach (string block in content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            string cleaned = block.Trim().Replace("\r\n", "\n").Replace('\n', ' ');
            if (cleaned.Length > 0) yield return cleaned;
        }
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
