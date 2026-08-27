using System.Globalization;
using System.Text.Json;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>Pure JSON→model parsing of the LLM output. Throws a descriptive exception when the shape is
/// invalid; tested against StaticMockLlmProvider output so a malformed mock proves the parser fails fast.</summary>
public static class BiddingJsonParser
{
    public static BiddingProposal Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(Normalize(json));
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

    private static string? Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>LLMs sometimes wrap the JSON answer in Markdown code fences (```json … ```); strip them so
    /// the response parses as raw JSON. Only a single leading fence block is removed.</summary>
    private static string Normalize(string json)
    {
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        int firstNewline = trimmed.IndexOf('\n');
        int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline <= 0 || lastFence <= firstNewline) return trimmed;
        return trimmed[(firstNewline + 1)..lastFence].Trim();
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
