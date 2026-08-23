using System.Globalization;
using System.Text.Json;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>Pure JSON→model parsing of the LLM output. Throws a descriptive exception when the shape is
/// invalid; tested against StaticMockLlmProvider output so a malformed mock proves the parser fails fast.</summary>
public static class BiddingJsonParser
{
    public static BiddingProposal Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string title = Read(root, "title") ?? "Ofertă tehnică";
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

        List<BoqLine> boq = [];
        if (root.TryGetProperty("billOfQuantities", out JsonElement boqList) && boqList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement line in boqList.EnumerateArray())
            {
                int position = line.TryGetProperty("position", out JsonElement p) && p.TryGetInt32(out int positionValue) ? positionValue : boq.Count + 1;
                string description = Read(line, "description") ?? string.Empty;
                string unit = Read(line, "unit") ?? "buc";
                decimal quantity = Decimal(line, "quantity");
                decimal unitPrice = Decimal(line, "unitPrice");
                decimal total = Decimal(line, "total") == 0 ? quantity * unitPrice : Decimal(line, "total");
                boq.Add(new(position, description, unit, quantity, unitPrice, total));
            }
        }

        return new(title, sections, boq);
    }

    private static string? Read(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal Decimal(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetDecimal(out decimal result)
            ? result
            : 0m;
}
