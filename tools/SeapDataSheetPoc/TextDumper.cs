using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SeapDataSheetPoc;

/// <summary>Per-page text dump so the deterministic section rules are built on the real DF layout (never on
/// assumptions). Lines are reconstructed from word bounding boxes (grouped by vertical position) — this
/// preserves the heading/newline layout that <c>Page.Text</c> flattens. PdfPig is the library family the app
/// already uses (<c>SeapAttachmentTextReader</c>).</summary>
public static class TextDumper
{
    public static int Run(string corpusDir, string outDir)
    {
        Directory.CreateDirectory(outDir);
        foreach (string pdf in Directory.GetFiles(corpusDir, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
            string name = Path.GetFileNameWithoutExtension(pdf);
            var pageTexts = new List<string>();
            using PdfDocument document = PdfDocument.Open(pdf);
            foreach (Page page in document.GetPages())
                pageTexts.Add(DumpLines(page));

            string all = string.Join("\n\n=== PAGE BREAK ===\n\n", pageTexts.Select((text, i) => $"[page {i + 1}]\n{text}"));
            File.WriteAllText(Path.Combine(outDir, $"{name}.txt"), all, Encoding.UTF8);
            Console.WriteLine($"{name}: pages={document.NumberOfPages} chars={all.Length}");
        }
        return 0;
    }

    private static string DumpLines(Page page)
    {
        IEnumerable<Word> words = page.GetWords();
        // Group words that sit on the same visual line (rounded vertical position) and order them left→right.
        var lines = words
            .GroupBy(word => Math.Round(word.BoundingBox.Top))
            .OrderByDescending(group => group.Key)
            .Select(group => string.Join(' ', group.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text)));
        return string.Join('\n', lines).Trim();
    }

    /// <summary>Collapses runs of whitespace but preserves the original order/words — the text is kept verbatim,
    /// only layout whitespace is normalised.</summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var builder = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (!lastWasSpace) builder.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
        }
        return builder.ToString().Trim();
    }
}

