using System.Security.Cryptography;
using System.Text;

namespace SeapDataSheetPoc;

/// <summary>POC v2 runner: extracts every corpus Fișa de date, writes one JSON per file and verifies the
/// acceptance criteria. The report separates accepted headings, rejected candidates (with reason) and
/// expected-but-missing roles — 0 unmatched alone is never a success signal.</summary>
public static class DataSheetRunner
{
    public static int Run(string corpusDir, string resultsDir, bool verify)
    {
        Directory.CreateDirectory(resultsDir);
        string[] pdfs = Directory.GetFiles(corpusDir, "*.pdf", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToArray();
        if (pdfs.Length == 0)
        {
            Console.Error.WriteLine("corpus empty");
            return 2;
        }

        var rows = new List<string>();
        int failures = 0;
        foreach (string pdf in pdfs)
        {
            string name = Path.GetFileNameWithoutExtension(pdf);
            try
            {
                DataSheetExtraction first = DataSheetExtractor.Extract(pdf);
                File.WriteAllText(Path.Combine(resultsDir, $"{name}.json"), DataSheetJson.Write(first), Encoding.UTF8);

                bool deterministic = !verify;
                if (verify)
                {
                    DataSheetExtraction second = DataSheetExtractor.Extract(pdf);
                    deterministic = DataSheetJson.Canonical(first) == DataSheetJson.Canonical(second);
                }

                double coverage = Coverage(pdf, first);
                string rejectInfo = string.Join(";", first.RejectedHeadings
                    .GroupBy(rejection => rejection.Reason.Split('—')[0])
                    .Select(group => $"{group.Count()}x {group.Key}"));
                string anchors = AnchorsReport(first);
                bool regression = verify && Vi3Regression(first);

                Console.WriteLine($"  {name}: pages={first.PageCount} accepted={first.Sections.Count} " +
                    $"rejected={first.RejectedHeadings.Count} [{rejectInfo}] coverage={coverage:P0} det={deterministic} vi3={regression}");
                foreach (HeadingRejection rejection in first.RejectedHeadings.Take(5))
                    Console.WriteLine($"     x p{rejection.Page}: {Truncate(rejection.Text, 60)} - {rejection.Reason}");
                if (first.MissingExpectedRoles.Count > 0)
                    Console.WriteLine($"     ! expected-but-missing: {string.Join(", ", first.MissingExpectedRoles)}");
                Console.WriteLine($"     anchors: {anchors}");

                rows.Add(string.Join('|', name, Sha256(pdf), first.PageCount, first.Sections.Count,
                    first.RejectedHeadings.Count, rejectInfo, string.Join(";", first.MissingExpectedRoles).Replace('|', '/'),
                    anchors, $"{coverage:P0}", deterministic ? "ok" : "DIFFERS", regression ? "VI3-REGRESSION" : "ok"));

                if (verify && (!first.HasTextLayer || !deterministic || coverage < 0.97 || regression || first.MissingExpectedRoles.Count > 0))
                    failures++;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"  {name}: ERROR {exception}");
                rows.Add(string.Join('|', name, "ERR", exception.Message.Replace('|', '/'), "", "", "", "", "", "", ""));
                failures++;
            }
        }

        File.WriteAllText(Path.Combine(resultsDir, "verify-report.csv"), string.Join("\n", rows) + "\n", Encoding.UTF8);
        Console.WriteLine($"--- done: {pdfs.Length} files, {failures} failure(s); report at {resultsDir} ---");
        return failures == 0 ? 0 : 1;
    }

    private static double Coverage(string pdf, DataSheetExtraction result)
    {
        int captured = result.Sections.Sum(section => section.Text.Count(char.IsLetter)) + result.HeadingLetters;
        int total = CountLetters(pdf);
        return total == 0 ? 0 : (double)captured / total;
    }

    private static bool Vi3Regression(DataSheetExtraction result)
    {
        // POC v1 defect: "VI.3) Informatii suplimentare" matched inside the Section-III exclusion listing and
        // cut III.1.1. In a valid DF, SupplementaryInfo must open AFTER the procedure/conditions sections.
        int latestBefore = 0;
        foreach (string role in new[]
                 {
                     DataSheetRoles.FinancingPaymentConditions, DataSheetRoles.ContractConditions,
                     DataSheetRoles.SpecialContractExecutionConditions, DataSheetRoles.OfferReceiptDeadline,
                     DataSheetRoles.OfferLanguages, DataSheetRoles.ContractExecutionPersonnel
                 })
        {
            int? page = result.Sections.Where(section => section.NormalizedRole == role).Select(section => (int?)section.StartPage).Max();
            if (page.HasValue) latestBefore = Math.Max(latestBefore, page.Value);
        }
        return result.Sections
            .Where(section => section.NormalizedRole == DataSheetRoles.SupplementaryInfo)
            .Any(section => latestBefore > 0 && section.StartPage < latestBefore);
    }

    private static string AnchorsReport(DataSheetExtraction result)
    {
        (string Label, string[] Keywords)[] anchors =
        [
            ("garantie", ["garantie de participare", "garantia de participare", "garantie"]),
            ("experienta", ["experienta similara", "experienta"]),
            ("criteriu", ["criteriul de atribuire", "criterii de atribuire", "criteriu de atribuire", "pretul cel mai scazut"]),
            ("durata", ["durata contractului", "durata de executie", "termenul de executie", "durata"]),
            ("finantare", ["finantare", "fonduri", "financiar"])
        ];
        var lines = new List<string>();
        foreach ((string label, string[] keywords) in anchors)
        {
            var pages = result.Sections
                .Where(section => keywords.Any(keyword => DataSheetExtractor.FoldKeepLength(section.Text).Contains(keyword)))
                .Select(section => $"p{section.StartPage}")
                .Distinct()
                .ToList();
            lines.Add(pages.Count > 0 ? $"{label}:ok({string.Join(",", pages)})" : $"{label}:MISSING");
        }
        return string.Join(" ", lines);
    }

    private static int CountLetters(string pdf)
    {
        byte[] bytes = File.ReadAllBytes(pdf);
        using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);
        int count = 0;
        foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
            count += page.Text.Count(char.IsLetter);
        return count;
    }

    private static string Sha256(string path)
    {
        byte[] hash = SHA256.HashData(File.ReadAllBytes(path));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}

