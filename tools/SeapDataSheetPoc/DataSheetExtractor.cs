using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace SeapDataSheetPoc;

/// <summary>
/// Deterministic extraction of a SEAP „Fișa de date" PDF (POC v2). Headings are accepted ONLY when the code
/// starts a reconstructed visual line close to the left margin of the page's text area — mid-paragraph code
/// occurrences are rejected, never matched. Text is sliced per page in publication order; undelimited content
/// stays in the enclosing section; rejected candidates and missing expected roles are reported separately.
/// Deterministic: identical bytes → identical output.
/// </summary>
public static class DataSheetExtractor
{
    public const int CurrentExtractionVersion = 2;
    private const int MinTextLayerLetters = 200;
    private const double MarginTolerance = 48.0; // heading levels are indented (x≈60–80) vs text margin x≈40

    public static DataSheetExtraction Extract(string pdfPath) => Extract(File.ReadAllBytes(pdfPath));

    public static DataSheetExtraction Extract(byte[] bytes)
    {
        string sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var warnings = new List<string>();

        List<string> rawPages;
        List<List<VisualLine>> layoutLines;
        using (PdfDocument document = PdfDocument.Open(bytes))
        {
            rawPages = [];
            layoutLines = [];
            foreach (Page page in document.GetPages())
            {
                rawPages.Add(page.Text);
                layoutLines.Add(LayoutLines.Build(page));
            }
        }

        int pageCount = rawPages.Count;
        int headingLetters = 0;
        List<string> normPages = rawPages.Select(NormalizeWhitespace).ToList();
        string allNorm = string.Join(' ', normPages); // pages joined with a single space; offsets tracked below.
        int letters = allNorm.Count(char.IsLetter);
        if (letters < MinTextLayerLetters || pageCount == 0)
        {
            warnings.Add($"NeedsOcr: text layer too thin ({letters} letters over {pageCount} page(s)).");
            return new DataSheetExtraction("DataSheet", false, true, pageCount, CurrentExtractionVersion, 0, [], [], [], warnings, sha256);
        }

        // Global word tokens over the normalized stream (offsets into allNorm).
        List<Token> tokens = [];
        foreach (Match match in Regex.Matches(allNorm, @"\S+"))
            tokens.Add(new Token(match.Index, match.Length));

        int[] pageStart = new int[pageCount + 1];
        int cursor = 0;
        for (int i = 0; i < pageCount; i++)
        {
            pageStart[i] = cursor;
            cursor += normPages[i].Length + 1;
        }
        pageStart[pageCount] = allNorm.Length;


        // ---- 1) Visual line scan: accepted headings + rejected candidates -------------------------------
        var accepted = new List<HeadingCandidate>();
        var rejected = new List<HeadingRejection>();
        var unknownLeftCode = new Regex(
            @"\A(?:I|II|III|IV|V|VI)(?:\.[0-9]{1,3}){1,3}(?:\.?[ab])?\)?[\.: ]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var midLineCode = new Regex(
            @"(?<![A-Za-z0-9])(?:I|II|III|IV|V|VI)(?:\.[0-9]{1,3}){1,3}(?:\.?[ab])?\)?[\.: ]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            string normFolded = FoldKeepLength(normPages[pageIndex]);
            double margin = layoutLines[pageIndex].Count > 0 ? layoutLines[pageIndex].Min(line => line.Left) : 0;
            int lastSearch = -1;

            foreach (VisualLine line in layoutLines[pageIndex])
            {
                string foldedLine = FoldKeepLength(line.Text);
                if (foldedLine.Length == 0) continue;

                HeadingRule? best = null;
                int bestLength = 0;
                foreach (HeadingRule rule in HeadingRules.Rules)
                {
                    Match match = rule.Pattern.Match(foldedLine);
                    if (match.Success && match.Index == 0 && match.Length > bestLength)
                    {
                        best = rule;
                        bestLength = match.Length;
                    }
                }

                if (best is not null)
                {
                    if (line.Left > margin + MarginTolerance)
                    {
                        rejected.Add(new HeadingRejection(pageIndex + 1, Truncate(foldedLine, 70), $"heading not at left margin (x={line.Left:0.#}, margin={margin:0.#})"));
                        continue;
                    }

                    // Locate the heading in the page's flat text stream (codes are the citation/anchor).
                    string anchorToken = FoldKeepLength(line.Text.Split(' ', 2)[0]);
                    int pos = normFolded.IndexOf(anchorToken, Math.Max(0, lastSearch + 1), StringComparison.Ordinal);
                    if (pos < 0)
                        pos = normFolded.IndexOf(anchorToken, StringComparison.Ordinal);
                    if (pos < 0 || pos < lastSearch)
                    {
                        rejected.Add(new HeadingRejection(pageIndex + 1, Truncate(foldedLine, 70), "layout/flat stream mismatch — heading kept inside enclosing section"));
                        continue;
                    }
                    lastSearch = pos;
                    accepted.Add(new HeadingCandidate(best, pageIndex, pos, Math.Min(bestLength, Math.Max(0, normFolded.Length - pos))));
                }
                else
                {
                    bool atMargin = line.Left <= margin + MarginTolerance;
                    Match unknown = unknownLeftCode.Match(foldedLine);
                    if (unknown.Success && atMargin)
                        rejected.Add(new HeadingRejection(pageIndex + 1, Truncate(foldedLine, 70), "unknown left-margin code"));
                    else if (midLineCode.IsMatch(foldedLine.Length > 12 ? foldedLine[6..] : string.Empty))
                        rejected.Add(new HeadingRejection(pageIndex + 1, Truncate(foldedLine, 70), "mid-paragraph code occurrence"));
                }
            }
        }

        accepted.Sort((a, b) =>
        {
            int byPage = a.Page.CompareTo(b.Page);
            return byPage != 0 ? byPage : a.CutPos.CompareTo(b.CutPos);
        });

        // ---- 2) Slice content between consecutive accepted headings --------------------------------------
        var sections = new List<DataSheetSection>();
        if (accepted.Count == 0)
        {
            sections.Add(new DataSheetSection("DOC", "DocumentIntegral", "Text integral", 1, pageCount, null, allNorm.Trim()));
            warnings.Add("no accepted headings — whole document kept as one section.");
        }
        else
        {
            int firstCutGlobal = GlobalOffset(pageStart, accepted[0].Page, accepted[0].CutPos);
            string prologue = allNorm[0..firstCutGlobal].Trim();
            if (prologue.Length > 0)
                sections.Add(new DataSheetSection("COVER", "DocumentHeader", "Copertă / instrucțiuni", 1, accepted[0].Page + 1, null, prologue));

            int currentLot = 0;
            for (int i = 0; i < accepted.Count; i++)
            {
                HeadingCandidate heading = accepted[i];
                int cutGlobal = GlobalOffset(pageStart, heading.Page, heading.CutPos);
                int endGlobal = i + 1 < accepted.Count
                    ? GlobalOffset(pageStart, accepted[i + 1].Page, accepted[i + 1].CutPos)
                    : allNorm.Length;
                int contentGlobal = Math.Min(allNorm.Length, cutGlobal + heading.Consumed);
                if (contentGlobal > endGlobal) contentGlobal = endGlobal;
                if (contentGlobal > cutGlobal) headingLetters += allNorm[cutGlobal..contentGlobal].Count(char.IsLetter);
                string content = allNorm[contentGlobal..endGlobal].Trim(' ', '\t', '\r', '\n', ':', ')', '.').Trim();
                if (contentGlobal > cutGlobal && content.Length == 0)
                    content = string.Empty;

                if (heading.Rule.Role == DataSheetRoles.LotHeader) currentLot++;
                bool isLotField = heading.Rule.RawCode.StartsWith("II.2", StringComparison.Ordinal);
                sections.Add(new DataSheetSection(
                    heading.Rule.RawCode,
                    heading.Rule.Role,
                    heading.Rule.Title,
                    heading.Page + 1,
                    Math.Max(heading.Page + 1, PageOfGlobal(pageStart, Math.Max(cutGlobal, endGlobal - 1))),
                    isLotField ? Math.Max(1, currentLot) : null,
                    content));
            }
        }


        // ---- 3) Expected-role checks (printed-code independent) ------------------------------------------
        var presentRoles = sections.Select(section => section.NormalizedRole).ToHashSet();
        string[] requiredCore = [DataSheetRoles.ContractingAuthorityMainActivity, DataSheetRoles.EstimatedValue, DataSheetRoles.ContestationProcedure];
        List<string> missing = requiredCore.Where(role => !presentRoles.Contains(role)).ToList();

        if (!presentRoles.Contains(DataSheetRoles.ParticipationConditions)
            && !presentRoles.Contains(DataSheetRoles.TechnicalProfessionalCapacity))
            warnings.Add("no conditions sections detected (III.*) — the authority may not have published selection conditions.");

        return new DataSheetExtraction(
            "DataSheet", true, false, pageCount, CurrentExtractionVersion, headingLetters,
            sections, rejected, missing, warnings, sha256);
    }

    // ----------------------------------------------------------------------------------------------------
    private static int GlobalOffset(int[] pageStart, int pageIndex, int pageOffset) =>
        Math.Min(pageStart[^1], pageStart[pageIndex] + pageOffset);

    private static int PageOfGlobal(int[] pageStart, int globalIndex)
    {
        for (int i = pageStart.Length - 2; i >= 0; i--)
            if (globalIndex >= pageStart[i]) return i + 1;
        return 1;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static string NormalizeWhitespace(string text)
    {
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

    /// <summary>Folds Romanian/Western diacritics to ASCII, one char → one char, so offsets stay identical.</summary>
    internal static string FoldKeepLength(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            builder.Append(c switch
            {
                'ă' or 'â' => 'a', 'Ă' or 'Â' => 'A', 'î' => 'i', 'Î' => 'I',
                'ș' or 'ş' => 's', 'Ș' or 'Ş' => 'S', 'ț' or 'ţ' => 't', 'Ț' or 'Ţ' => 'T',
                'á' or 'à' or 'ä' or 'ã' => 'a', 'Á' or 'À' or 'Ä' or 'Ã' => 'A',
                'é' or 'è' or 'ë' or 'ê' => 'e', 'É' or 'È' or 'Ë' or 'Ê' => 'E',
                'í' or 'ì' or 'ï' => 'i', 'Í' or 'Ì' or 'Ï' => 'I',
                'ó' or 'ò' or 'ö' or 'õ' or 'ô' => 'o', 'Ó' or 'Ò' or 'Ö' or 'Õ' or 'Ô' => 'O',
                'ú' or 'ù' or 'ü' or 'û' => 'u', 'Ú' or 'Ù' or 'Ü' or 'Û' => 'U',
                'ç' => 'c', 'Ç' => 'C',
                _ => c
            });
        }
        return builder.ToString();
    }

    private sealed record HeadingCandidate(HeadingRule Rule, int Page, int CutPos, int Consumed);
}

/// <summary>Reconstructs visual text lines from word coordinates (baseline clustering), ordered top→bottom.
/// The left coordinate of each line is kept so headings must start near the page's text-area margin.</summary>
internal static class LayoutLines
{
    public static List<VisualLine> Build(Page page)
    {
        Word[] words = page.GetWords().ToArray();
        var ordered = words.OrderByDescending(word => word.BoundingBox.Bottom).ThenBy(word => word.BoundingBox.Left).ToArray();
        var lines = new List<VisualLine>();
        var buffer = new List<Word>();
        double lineBottom = 0;

        foreach (Word word in ordered)
        {
            if (buffer.Count == 0)
            {
                buffer.Add(word);
                lineBottom = word.BoundingBox.Bottom;
                continue;
            }
            if (Math.Abs(word.BoundingBox.Bottom - lineBottom) <= 2.5)
            {
                buffer.Add(word);
            }
            else
            {
                lines.Add(Finish(buffer));
                buffer = [word];
                lineBottom = word.BoundingBox.Bottom;
            }
        }
        if (buffer.Count > 0) lines.Add(Finish(buffer));
        return lines;
    }

    private static VisualLine Finish(List<Word> words)
    {
        words.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
        string text = string.Join(' ', words.Select(word => word.Text));
        double left = words[0].BoundingBox.Left;
        return new VisualLine(0, 0, 0, left, text);
    }
}
