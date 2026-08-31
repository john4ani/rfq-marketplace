using System.Globalization;
using System.Text;
using System.Text.Json;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Microsoft.Extensions.Logging;

namespace Marketplace.Infrastructure.Seap;

/// <summary>
/// Generates the structured AI extraction („Sinteză AI" bullets + decision-critical detail fields) for a
/// SEAP tender from the notice data the importer already holds (title, CPV, estimated value, buyer, county,
/// deadlines, description when present) plus — when the ingestion pipeline has already processed them — the
/// text of the published attachments (Fișa de Date / Caiet de Sarcini). Uses the shared <see cref="ILlmProvider"/>
/// (Claude; <c>StaticMockLlmProvider</c> in tests). Enrichment is never a gate: any LLM/parse failure
/// degrades to null and the tender still imports without AI details.
/// </summary>
public sealed partial class SeapSummaryService(
    ILlmProvider llmProvider,
    ILogger<SeapSummaryService> logger) : ISeapSummaryGenerator
{
    private const int MaxBullets = 5;
    private const int MaxBulletLength = 140;

    // Romanian number grouping („2.420.466 RON") — the notice currency is always RON and the prompt is Romanian.
    private static readonly NumberFormatInfo RomanianNumbers = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ","
    };

    private const string SystemPrompt =
        "Ești un expert în achiziții publice (SEAP/SICAP România) și analiză juridică B2B.\n" +
        "Rolul tău este să analizezi textul unei licitații publice (sau extrase din Fișa de Date/Caietul de Sarcini) " +
        "și să extragi date structurate vitale pentru decizia de participare a unei companii.\n\n" +
        "REGULI STRICTE:\n" +
        "1. Analizează textul furnizat în <document_text>.\n" +
        "2. Extrage valorile exacte pentru câmpurile solicitate. Dacă o informație lipsește cu desăvârșire, " +
        "returnează null pentru acel câmp, nu inventa date.\n" +
        "3. Formatează sumele financiar clar (ex: \"120.000 RON\").\n" +
        "4. Pentru \"summaryPoints\", generează maxim 5 bullet-uri scurte, la obiect (max 140 caractere fiecare), " +
        "care să conțină cele mai importante bariere de intrare (garanții, experiență cerută, criteriu de atribuire, termene).\n" +
        "5. Trebuie să răspunzi EXCLUSIV cu un obiect JSON valid, fără niciun alt text înainte sau după, folosind schema de mai jos.\n\n" +
        "Structura JSON (Function Calling / Structured Output):\n" +
        "{\n" +
        "  \"summaryPoints\": [\"string (bullet point 1)\", \"string (bullet point 2)\"],\n" +
        "  \"description\": \"string (rezumatul achiziției în 2-3 propoziții)\",\n" +
        "  \"awardCriterion\": \"string (ex: Prețul cel mai scăzut / Cel mai bun raport calitate-preț - cu ponderi dacă există)\",\n" +
        "  \"participationGuarantee\": \"string (suma și valuta, ex: 50.000 RON)\",\n" +
        "  \"fundingSource\": \"string (ex: PNRR, Fonduri Europene, Buget Local - dacă este specificat)\",\n" +
        "  \"contractDuration\": \"string (ex: 12 luni, 60 de zile)\",\n" +
        "  \"qualificationRequirements\": \"string (rezumatul cerințelor de experiență similară/cifră de afaceri)\",\n" +
        "  \"keyPersonnel\": \"string (lista scurtă a experților cheie solicitați, ex: Manager de proiect, Inginer RTE)\"\n" +
        "}";

    public async Task<SeapAiExtraction?> GenerateAsync(
        SeapTender tender, string? attachmentText, CancellationToken cancellationToken)
    {
        try
        {
            string userPrompt =
                $"<document_text>\n{BuildTenderText(tender, attachmentText)}\n</document_text>\n\n" +
                "Extrage datele structurate conform schemei JSON.";
            string llmJson = await llmProvider.CompleteAsync(SystemPrompt, userPrompt, cancellationToken);
            SeapAiExtraction? extraction = ParseExtraction(llmJson);
            if (extraction is null)
                LogSummaryUnparseable(logger, tender.SeapExternalId);
            return extraction;
        }
        catch (Exception exception)
        {
            LogSummaryFailed(logger, tender.SeapExternalId, exception);
            return null;
        }
    }

    private static string BuildTenderText(SeapTender tender, string? attachmentText)
    {
        var text = new StringBuilder();
        text.Append("Titlu: ").Append(tender.Title).Append('\n');
        if (!string.IsNullOrWhiteSpace(tender.Description))
            text.Append("Descriere: ").Append(tender.Description).Append('\n');
        if (tender.CpvCodes.Count > 0)
            text.Append("Coduri CPV: ").Append(string.Join(", ", tender.CpvCodes)).Append('\n');
        if (tender.CpvNames.Count > 0)
            text.Append("Denumiri CPV: ").Append(string.Join(", ", tender.CpvNames)).Append('\n');
        if (tender.EstimatedValue.HasValue)
            text.Append("Valoare estimată: ")
                .Append(tender.EstimatedValue.Value.ToString("N0", RomanianNumbers))
                .Append(' ').Append(tender.Currency).Append('\n');
        text.Append("Instituție contractantă: ").Append(tender.BuyerName).Append('\n');
        if (!string.IsNullOrWhiteSpace(tender.County))
            text.Append("Județ: ").Append(tender.County).Append('\n');
        text.Append("Publicat: ").Append(tender.PublicationDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append('\n');
        text.Append("Termen depunere: ").Append(tender.Deadline.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)).Append('\n');
        if (!string.IsNullOrWhiteSpace(attachmentText))
            text.Append("\nEXTRAS DIN FIȘA DE DATE / CAIETUL DE SARCINI:\n").Append(attachmentText.Trim());
        return text.ToString();
    }

    /// <summary>Parses the LLM output, tolerating Markdown prose that wraps the JSON object. Returns null when
    /// the output is unparseable or yields nothing useful (all fields null and no summary bullets).</summary>
    private static SeapAiExtraction? ParseExtraction(string llmJson)
    {
        string normalized = Normalize(llmJson);
        using JsonDocument? document = ParseJsonObject(normalized);
        if (document is null) return null;
        JsonElement root = document.RootElement;

        string? description = ReadString(root, "description");
        string? awardCriterion = ReadString(root, "awardCriterion");
        string? participationGuarantee = ReadString(root, "participationGuarantee");
        string? fundingSource = ReadString(root, "fundingSource");
        string? contractDuration = ReadString(root, "contractDuration");
        string? qualificationRequirements = ReadString(root, "qualificationRequirements");
        string? keyPersonnel = ReadString(root, "keyPersonnel");

        List<string> bullets = [];
        if (root.TryGetProperty("summaryPoints", out JsonElement points) && points.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement point in points.EnumerateArray())
            {
                if (bullets.Count >= MaxBullets) break;
                string? value = point.GetString();
                if (string.IsNullOrWhiteSpace(value)) continue;
                string bullet = value.Trim();
                if (bullet.Length > MaxBulletLength) bullet = bullet[..MaxBulletLength] + "…";
                bullets.Add(bullet);
            }
        }

        if (bullets.Count == 0 && description is null && awardCriterion is null
            && participationGuarantee is null && fundingSource is null && contractDuration is null
            && qualificationRequirements is null && keyPersonnel is null)
            return null;

        return new SeapAiExtraction(description, awardCriterion, participationGuarantee, fundingSource,
            contractDuration, qualificationRequirements, keyPersonnel, bullets);
    }

    /// <summary>Strips a Markdown code fence around the JSON body when present (a truncated response without a
    /// closing fence keeps the body to the end of the input).</summary>
    private static string Normalize(string json)
    {
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        int firstNewline = trimmed.IndexOf('\n');
        if (firstNewline <= 0) return trimmed;
        int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        string body = lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence]
            : trimmed[(firstNewline + 1)..];
        return body.Trim();
    }

    /// <summary>Parses the normalized response; when it is not standalone JSON it falls back to the outermost
    /// JSON object (first „{" to last „}"). Returns null when no JSON object can be extracted.</summary>
    private static JsonDocument? ParseJsonObject(string normalized)
    {
        try
        {
            return JsonDocument.Parse(normalized);
        }
        catch (JsonException)
        {
            int firstOpen = normalized.IndexOf('{');
            int lastClose = normalized.LastIndexOf('}');
            if (firstOpen < 0 || lastClose <= firstOpen) return null;
            try
            {
                return JsonDocument.Parse(normalized[firstOpen..(lastClose + 1)]);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    [LoggerMessage(EventId = 5120, Level = LogLevel.Warning, Message = "SEAP tender {SeapId} LLM summary output could not be parsed; tender stays without AI details.")]
    private static partial void LogSummaryUnparseable(ILogger logger, string seapId);
    [LoggerMessage(EventId = 5121, Level = LogLevel.Warning, Message = "SEAP tender {SeapId} AI summary generation failed; tender stays without AI details.")]
    private static partial void LogSummaryFailed(ILogger logger, string seapId, Exception exception);
}

