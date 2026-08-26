using System.Text;

namespace Marketplace.Infrastructure.Documents;

/// <summary>
/// Pure conversion of an Azure Document Intelligence extraction (pages of lines + structural tables)
/// into Markdown, so the text/tables can be fed to an LLM (or stored for RAG) in a stable format.
/// Tables become pipe tables; pages become headings. No I/O, directly unit-testable.
/// </summary>
public static class MarkdownConverter
{
    public static string ToMarkdown(DocumentExtractionResult extraction)
    {
        StringBuilder markdown = new();

        foreach (ExtractedPage page in extraction.Pages)
        {
            if (markdown.Length > 0) markdown.AppendLine();
            markdown.Append("## Pagina ").Append(page.PageNumber).AppendLine();

            foreach (ExtractedTable table in page.Tables)
            {
                if (!string.IsNullOrWhiteSpace(table.Caption))
                    markdown.Append("**").Append(table.Caption).AppendLine("**");
                AppendTable(markdown, table.Rows);
            }

            foreach (string line in page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                markdown.AppendLine(line);
        }

        return markdown.ToString().Trim();
    }

    private static void AppendTable(StringBuilder markdown, IReadOnlyCollection<IReadOnlyCollection<string>> rows)
    {
        if (rows.Count == 0) return;
        int columns = Math.Max(1, rows.Max(row => row.Count));

        markdown.AppendLine(RenderRow(rows.First()));
        markdown.AppendLine(RenderRow(Enumerable.Repeat("---", columns)));
        foreach (IReadOnlyCollection<string> row in rows.Skip(1))
        {
            List<string> cells = row.ToList();
            while (cells.Count < columns) cells.Add(string.Empty);
            markdown.AppendLine(RenderRow(cells));
        }
    }

    private static string RenderRow(IEnumerable<string> cells) =>
        "|" + string.Join("|", cells.Select(cell => " " + cell + " ")) + "|";
}
