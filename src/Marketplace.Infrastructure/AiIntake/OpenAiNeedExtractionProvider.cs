using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.Rfqs;

namespace Marketplace.Infrastructure.AiIntake;

// General Procurement interpreter: domain-light commercial extraction. It owns its prompt, JSON schema and mapping,
// and reuses the shared OpenAI Responses transport. It never carries Construction depth (ADR-002).
internal sealed class OpenAiNeedExtractionProvider(OpenAiResponsesTransport transport) : IGeneralNeedInterpreter
{
    public bool IsAvailable => transport.IsAvailable;

    public async Task<GeneralNeedInterpretation> InterpretAsync(GeneralNeedInterpretationRequest context, CancellationToken cancellationToken)
    {
        OpenAiStructuredResponse response = await transport.SendAsync(BuildRequest(context), cancellationToken);
        AiExtraction extraction = JsonSerializer.Deserialize<AiExtraction>(response.OutputText, JsonOptions)
            ?? throw new InvalidOperationException("AI intake structured output is empty.");
        return Map(context, extraction, response.InputTokens, response.OutputTokens);
    }

    private static OpenAiStructuredRequest BuildRequest(GeneralNeedInterpretationRequest context)
    {
        string categories = string.Join("\n", context.Categories.Select(item => $"- {item.Slug}: {item.Name}"));
        string turns = context.Turns.Count == 0 ? "Nicio clarificare încă." : string.Join("\n",
            context.Turns.Select(turn => $"Î: {turn.Question}\nR: {turn.Answer}"));
        string instructions = """
            Ești asistentul general de achiziții CompaSpot. Nu ești expert tehnic într-o industrie și nu faci research web.
            Fluxul obligatoriu este: clarificare, apoi o propunere comercială editabilă pentru confirmare umană. Nu publica
            și nu prezenta propunerea drept specificație tehnică finală.

            1. Clarificare: identifică informația cu cel mai mare impact asupra necesarului și pune exact o întrebare
            scurtă pe rând. Clarifică doar ce este necesar pentru a cere o ofertă utilă, de exemplu produsul sau
            serviciul dorit, cantitatea, compatibilitatea, specificația, locul ori termenul. Cât timp lipsește o
            informație esențială, clarification nu poate fi null.
            2. Analiză: după ce există suficiente date comerciale, extrage numai ceea ce utilizatorul a spus sau poate fi
            formulat ca întrebare comercială. Nu ai acces la web search. Nu inventa compatibilități, coduri OEM, stoc,
            prețuri, certificări, capabilități ale furnizorilor, surse, URL-uri, cantități, mărci sau standarde.
            3. Propunere: creează o listă editabilă de produse și/sau servicii numai când informația este suficientă.
            Pentru o lucrare completă păstrează serviciul ca opțiune; nu deduce materiale sau componente necerute.
            Marchează explicit presupunerile și aspectele de verificat.
            4. Mod sugerat: dacă descrierea pare un proiect de construcții, setează suggestedMode la ConstructionProjects;
            nu schimba fluxul și nu transforma cererea. În toate celelalte cazuri setează GeneralProcurement sau null.
            5. Confirmare: când propunerea poate fi verificată de utilizator, setează clarification la null. Niciodată
            nu considera cererea publicată sau aprobată.

            categorySlug trebuie să fie null sau unul dintre slug-urile furnizate. Scrie toate textele în română.
            Pentru fiecare poziție cu cantitate, unit trebuie să fie una dintre valorile permise de schemă; nu lăsa unit null.
            Pentru servicii discrete fără altă unitate potrivită, folosește Piece.
            """;
        string input = $"""
            Descriere inițială:
            {context.InitialInput}

            Moment curent UTC: {context.Now:O}
            Profil livrare implicit: {context.BuyerCity}, {context.BuyerCounty}; rază {context.BuyerRadiusKm} km.
            Categorii valide:
            {categories}

            Clarificări anterioare:
            {turns}
            """;
        return new OpenAiStructuredRequest(instructions, input, "procurement_need", Schema, context.BuyerId);
    }

    private GeneralNeedInterpretation Map(GeneralNeedInterpretationRequest context, AiExtraction extraction, int inputTokens, int outputTokens)
    {
        Dictionary<string, IntakeCategory> categories = context.Categories.ToDictionary(item => item.Slug, StringComparer.OrdinalIgnoreCase);
        IntakeCategory? primary = Resolve(extraction.PrimaryCategorySlug, categories);
        AssistedRequirementLine[] lines = extraction.Lines.Take(25).Select(line => new AssistedRequirementLine(
            Enum.TryParse(line.Kind, true, out RequirementKind kind) ? kind : RequirementKind.Product,
            Clean(line.Name, 160, "Poziție de clarificat"), CleanNullable(line.Specification, 1000),
            line.Quantity > 0 ? line.Quantity : null,
            Enum.TryParse(line.Unit, true, out UnitOfMeasure unit) ? unit : null,
            line.AlternativesAccepted, Resolve(line.CategorySlug, categories)?.Id ?? primary?.Id)).ToArray();
        if (lines.Length == 0)
            lines = [new(RequirementKind.Product, "Poziție de clarificat", null, null, null, true, primary?.Id)];

        AssistedClarification? clarification = extraction.Clarification is null ? null : new(
            Enum.TryParse(extraction.Clarification.Field, true, out ClarificationField field) ? field : ClarificationField.Specification,
            Clean(extraction.Clarification.Question, 300, "Ce detaliu comercial mai trebuie precizat?"),
            extraction.Clarification.Choices.Where(item => !string.IsNullOrWhiteSpace(item)).Take(8).ToArray());
        ProcurementInputType inputType = Enum.TryParse(extraction.InputType, true, out ProcurementInputType parsed)
            ? parsed : ProcurementInputType.FreeText;
        DateTimeOffset quotationDeadline = ParseQuotationDeadline(extraction.QuotationDeadline, context.Now);
        var draft = new AssistedNeedDraft(inputType, Clean(extraction.Title, 180, "Cerere nouă"),
            CleanNullable(extraction.ProjectName, 180), CleanNullable(extraction.Description, 4000), primary?.Id,
            Clean(extraction.DeliveryAddress, 300, string.Empty),
            Clean(extraction.DeliveryLocality, 120, context.BuyerCity), Clean(extraction.County, 80, context.BuyerCounty),
            context.BuyerRadiusKm, ParsePreferredDeliveryDate(extraction.PreferredDeliveryDate), quotationDeadline,
            extraction.AlternativeProductsAccepted, extraction.AllowSplit, extraction.DeliveryRequired,
            extraction.EstimatedBudget is > 0 ? extraction.EstimatedBudget : null,
            CleanNullable(extraction.BuyerNotes, 1000), lines);
        SuggestedProcurementMode? suggestedMode = Enum.TryParse(extraction.SuggestedMode, true,
            out SuggestedProcurementMode parsedSuggestedMode) ? parsedSuggestedMode : null;
        return new(AssistedIntakeMode.OpenAi, transport.ProviderName, transport.Model, transport.PromptVersion, draft,
            extraction.Assumptions.Where(NotBlank).Take(20).ToArray(),
            extraction.Uncertainties.Where(NotBlank).Take(20).ToArray(), [], clarification, inputTokens, outputTokens,
            suggestedMode);
    }

    private static IntakeCategory? Resolve(string? slug, Dictionary<string, IntakeCategory> categories) =>
        slug is not null && categories.TryGetValue(slug, out IntakeCategory? value) ? value : null;
    private static string Clean(string? value, int length, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, length)];
    private static string? CleanNullable(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Clean(value, length, string.Empty);
    private static bool NotBlank(string value) => !string.IsNullOrWhiteSpace(value);

    private static DateOnly? ParsePreferredDeliveryDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateOnly date)
            ? date
            : null;

    private static DateTimeOffset ParseQuotationDeadline(string? value, DateTimeOffset now)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out DateTimeOffset deadline)
            && deadline > now)
            return deadline;

        return now.AddDays(2);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonNode Schema = JsonNode.Parse("""
    {
      "type":"object","additionalProperties":false,
      "required":["inputType","title","projectName","description","primaryCategorySlug","deliveryAddress","deliveryLocality","county","preferredDeliveryDate","quotationDeadline","alternativeProductsAccepted","allowSplit","deliveryRequired","estimatedBudget","buyerNotes","lines","assumptions","uncertainties","clarification","suggestedMode"],
      "properties":{
        "inputType":{"type":"string","enum":["FreeText","ShoppingList","ProjectDescription"]},
        "title":{"type":"string"},"projectName":{"type":["string","null"]},"description":{"type":["string","null"]},
        "primaryCategorySlug":{"type":["string","null"]},"deliveryAddress":{"type":"string"},"deliveryLocality":{"type":"string"},"county":{"type":"string"},
        "preferredDeliveryDate":{"type":["string","null"]},"quotationDeadline":{"type":"string"},
        "alternativeProductsAccepted":{"type":"boolean"},"allowSplit":{"type":"boolean"},"deliveryRequired":{"type":"boolean"},
        "estimatedBudget":{"type":["number","null"]},"buyerNotes":{"type":["string","null"]},
        "lines":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["kind","name","specification","quantity","unit","alternativesAccepted","categorySlug"],"properties":{"kind":{"type":"string","enum":["Product","Service"]},"name":{"type":"string"},"specification":{"type":["string","null"]},"quantity":{"type":["number","null"]},"unit":{"anyOf":[{"type":"null"},{"type":"string","enum":["Piece","Pack","Bag","Kilogram","Tonne","Metre","SquareMetre","CubicMetre","Litre","Pallet","Other"]}]},"alternativesAccepted":{"type":"boolean"},"categorySlug":{"type":["string","null"]}}}},
        "assumptions":{"type":"array","items":{"type":"string"}},"uncertainties":{"type":"array","items":{"type":"string"}},
        "clarification":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"required":["field","question","choices"],"properties":{"field":{"type":"string","enum":["Category","Quantity","Specification","ProjectScope","DeliveryAddress","DeliveryDate"]},"question":{"type":"string"},"choices":{"type":"array","items":{"type":"string"}}}}]},
        "suggestedMode":{"anyOf":[{"type":"null"},{"type":"string","enum":["GeneralProcurement","ConstructionProjects"]}]}
      }
    }
    """)!;

    private sealed record AiExtraction(string InputType, string Title, string? ProjectName, string? Description,
        string? PrimaryCategorySlug, string DeliveryAddress, string DeliveryLocality, string County,
        string? PreferredDeliveryDate, string? QuotationDeadline, bool AlternativeProductsAccepted,
        bool AllowSplit, bool DeliveryRequired, decimal? EstimatedBudget, string? BuyerNotes,
        IReadOnlyCollection<AiLine> Lines, IReadOnlyCollection<string> Assumptions,
        IReadOnlyCollection<string> Uncertainties,
        AiClarification? Clarification, string? SuggestedMode);
    private sealed record AiLine(string Kind, string Name, string? Specification, decimal? Quantity,
        string? Unit, bool AlternativesAccepted, string? CategorySlug);
    private sealed record AiClarification(string Field, string Question, IReadOnlyCollection<string> Choices);
}
