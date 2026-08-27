using System.Globalization;
using System.Text.Json;
using Marketplace.Application.AutoBidding;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>Pure JSON→model parsing of the LLM output. Throws a descriptive exception when the shape is
/// invalid; tested against StaticMockLlmProvider output so a malformed mock proves the parser fails fast.</summary>
public static class BiddingJsonParser
{
    public static BiddingProposal Parse(string json)
    {
        string normalized = Normalize(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(normalized);
        }
        catch (JsonException exception)
        {
            bool looksTruncated = normalized.TrimStart().StartsWith('{') && !normalized.TrimEnd().EndsWith('}');
            throw new AutoBiddingPipelineException(422,
                looksTruncated
                    ? "Răspunsul LLM a fost trunchiat (JSON incomplet). Reîncearcă generarea propunerii."
                    : $"Răspunsul LLM nu este JSON valid: {exception.Message}");
        }
        using (document)
        {
            JsonElement root = document.RootElement;

            string title = Read(root, "title") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
                throw new InvalidOperationException("Răspunsul LLM nu conține un titlu de propunere.");

            List<ProposalSection> sections = [];
            if (root.TryGetProperty("technicalProposal", out JsonElement proposal))
            {
                if (proposal.TryGetProperty("sections", out JsonElement sectionList) && sectionList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement section in sectionList.EnumerateArray())
                    {
                        string heading = Read(section, "heading") ?? "Secțiune";
                        string content = Read(section, "content") ?? string.Empty;
                        sections.Add(new(heading, content));
                    }
                }
            }
            if (sections.Count == 0)
                throw new InvalidOperationException("Răspunsul LLM nu conține secțiuni pentru propunerea tehnică.");

            List<BoqLine> boq = [];
            if (root.TryGetProperty("billOfQuantities", out JsonElement boqList) && boqList.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement line in boqList.EnumerateArray())
                {
                    int position = line.TryGetProperty("position", out JsonElement p) && p.TryGetInt32(out int positionValue) ? positionValue : boq.Count + 1;
                    string description = Read(line, "description") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(description))
                        throw new InvalidOperationException($"Linia {position} din lista de cantități nu are descriere.");
                    string unit = Read(line, "unit") ?? "buc";
                    decimal quantity = RequiredDecimal(line, "quantity", position);
                    decimal unitPrice = RequiredDecimal(line, "unitPrice", position);
                    decimal total = OptionalDecimal(line, "total") ?? quantity * unitPrice;
                    boq.Add(new(position, description, unit, quantity, unitPrice, total));
                }
            }
            if (boq.Count == 0)
                throw new InvalidOperationException("Răspunsul LLM nu conține nicio linie pentru lista de cantități.");

            List<FinancialSummaryLine> financialSummary = [];
            if (root.TryGetProperty("financialSummary", out JsonElement summaryList) && summaryList.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement line in summaryList.EnumerateArray())
                {
                    string label = Read(line, "label") ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    string? note = Read(line, "note");
                    financialSummary.Add(new(label, OptionalDecimal(line, "value") ?? 0m,
                        string.IsNullOrWhiteSpace(note) ? null : note));
                }
            }

            return new(title, sections, boq, financialSummary);
        }
    }

    private static string? Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// LLMs sometimes wrap the JSON answer in Markdown code fences (```json … ```) and occasionally the
    /// closing fence is missing because the response was truncated. Strip a leading fence line whenever it
    /// is present (even without a closing fence) so the JSON body can still be parsed or reported as
    /// truncated with a clear message. Only the first fence block is consumed.
    /// </summary>
    private static string Normalize(string json)
    {
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        int firstNewline = trimmed.IndexOf('\n');
        if (firstNewline <= 0) return trimmed;
        int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        // Keep everything after the opening fence line; drop a trailing fence only when it is a real
        // closing fence (after the opening one), otherwise the JSON body continues to the end of the input.
        string body = lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence]
            : trimmed[(firstNewline + 1)..];
        return body.Trim();
    }

    private static decimal RequiredDecimal(JsonElement element, string property, int position)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || !value.TryGetDecimal(out decimal result))
            throw new InvalidOperationException($"Linia {position} din lista de cantități nu are o valoare validă pentru „{property}”.");
        return result;
    }

    private static decimal? OptionalDecimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetDecimal(out decimal result) ? result : null;
}
