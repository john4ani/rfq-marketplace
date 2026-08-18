using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.Rfqs;

namespace Marketplace.Infrastructure.AiIntake;

// Construction Projects interpreter: a distinct interpretation workflow from the general one. It understands the
// construction PROJECT (type, scope, dimensions), asks one construction-specific clarification at a time, and derives
// materials/works with visible, non-certified assumptions. It reuses the shared OpenAI transport (Task 10 §14) but
// never the general prompt/schema (§13). MVP bounds: procurement assistance only — no engineering certification,
// no Passive House, no structural/thermal calculation (§17/§34).
internal sealed class OpenAiConstructionProjectInterpreter(OpenAiResponsesTransport transport) : IConstructionProjectInterpreter
{
    public bool IsAvailable => transport.IsAvailable;

    public async Task<ConstructionProjectInterpretation> InterpretAsync(
        ConstructionProjectInterpretationRequest request, CancellationToken cancellationToken)
    {
        OpenAiStructuredResponse response = await transport.SendAsync(BuildRequest(request), cancellationToken);
        ProjectExtraction extraction = JsonSerializer.Deserialize<ProjectExtraction>(response.OutputText, JsonOptions)
            ?? throw new InvalidOperationException("AI project structured output is empty.");
        return Map(request, extraction, response.InputTokens, response.OutputTokens);
    }

    private static OpenAiStructuredRequest BuildRequest(ConstructionProjectInterpretationRequest request)
    {
        string categories = string.Join("\n", request.Categories.Select(item => $"- {item.Slug}: {item.Name}"));
        string turns = request.Turns.Count == 0 ? "Nicio clarificare încă." : string.Join("\n",
            request.Turns.Select(turn => $"Î: {turn.Question}\nR: {turn.Answer}"));
        string instructions = """
            Ești asistentul CompaSpot pentru proiecte de construcții. Rolul tău este să înțelegi întâi PROIECTUL de
            construcție descris de utilizator și abia apoi să derivi necesarul de achiziție. Nu ești inginer, arhitect
            sau verificator autorizat și nu emiți calcule certificate. Oferi asistență comercială pentru achiziții.

            1. Înțelegerea proiectului: identifică tipul lucrării (de exemplu acoperiș, anexă, gard, șapă), scopul și
            dimensiunile relevante. Rezumă pe scurt, în projectSummary, ce ai înțeles despre proiect, în limbaj clar.
            2. Clarificare: pune exact o singură întrebare de construcții pe rând, doar pentru informația cu cel mai mare
            impact asupra necesarului (de exemplu forma acoperișului, dimensiuni, structura existentă, finisajul dorit).
            Cât timp lipsește o informație critică pentru a estima necesarul, clarification nu poate fi null. Nu genera un
            formular cu zeci de câmpuri deodată.
            3. Derivarea necesarului: după ce ai suficiente date, propune o listă editabilă de materiale și/sau lucrări
            (lines). Poți estima cantități pe baza dimensiunilor, dar orice estimare este o PRESUPUNERE comercială, nu un
            calcul ingineresc. Marchează explicit în assumptions presupunerile importante (de exemplu forma presupusă,
            procent de pierderi/adaos, grosime presupusă, mod de interpretare a dimensiunilor). Pune în uncertainties ce
            rămâne de verificat. Nu inventa coduri, standarde, mărci, prețuri, stoc sau certificări.
            4. Limbaj: prudent, comercial. Nu prezenta rezultatul drept certificare structurală, arhitecturală sau de
            reglementare. Nu implementa standarde de casă pasivă, calcule termice sau structurale.
            5. Confirmare: când necesarul poate fi verificat de utilizator, setează clarification la null. Niciodată nu
            considera necesarul publicat sau aprobat; utilizatorul confirmă înainte de publicare.

            categorySlug trebuie să fie null sau unul dintre slug-urile furnizate (categorii de construcții). Scrie toate
            textele în română. Pentru fiecare poziție cu cantitate, unit trebuie să fie una dintre valorile permise de
            schemă; nu lăsa unit null. Pentru lucrări/servicii fără altă unitate potrivită, folosește Piece.
            """;
        string input = $"""
            Descrierea proiectului:
            {request.InitialInput}

            Moment curent UTC: {request.Now:O}
            Profil livrare implicit: {request.BuyerCity}, {request.BuyerCounty}; rază {request.BuyerRadiusKm} km.
            Categorii valide (construcții):
            {categories}

            Clarificări anterioare:
            {turns}
            """;
        return new OpenAiStructuredRequest(instructions, input, "construction_project", Schema, request.UserId, 3000);
    }

    private ConstructionProjectInterpretation Map(
        ConstructionProjectInterpretationRequest request, ProjectExtraction extraction, int inputTokens, int outputTokens)
    {
        Dictionary<string, IntakeCategory> categories = request.Categories.ToDictionary(item => item.Slug, StringComparer.OrdinalIgnoreCase);
        IntakeCategory? primary = Resolve(extraction.PrimaryCategorySlug, categories);
        AssistedRequirementLine[] lines = extraction.Lines.Take(40).Select(line => new AssistedRequirementLine(
            Enum.TryParse(line.Kind, true, out RequirementKind kind) ? kind : RequirementKind.Product,
            Clean(line.Name, 160, "Poziție de clarificat"), CleanNullable(line.Specification, 1000),
            line.Quantity > 0 ? line.Quantity : null,
            Enum.TryParse(line.Unit, true, out UnitOfMeasure unit) ? unit : null,
            line.AlternativesAccepted, Resolve(line.CategorySlug, categories)?.Id ?? primary?.Id)).ToArray();
        if (lines.Length == 0)
            lines = [new(RequirementKind.Product, "Poziție de clarificat", null, null, null, true, primary?.Id)];

        AssistedClarification? clarification = extraction.Clarification is null ? null : new(
            Enum.TryParse(extraction.Clarification.Field, true, out ClarificationField field) ? field : ClarificationField.ProjectScope,
            Clean(extraction.Clarification.Question, 300, "Ce detaliu al proiectului mai trebuie precizat?"),
            extraction.Clarification.Choices.Where(item => !string.IsNullOrWhiteSpace(item)).Take(8).ToArray());
        DateTimeOffset quotationDeadline = ParseQuotationDeadline(extraction.QuotationDeadline, request.Now);
        var draft = new AssistedNeedDraft(ProcurementInputType.ProjectDescription,
            Clean(extraction.Title, 180, "Proiect de construcții"),
            CleanNullable(extraction.ProjectName, 180), CleanNullable(extraction.Description, 4000), primary?.Id,
            Clean(extraction.DeliveryAddress, 300, string.Empty),
            Clean(extraction.DeliveryLocality, 120, request.BuyerCity), Clean(extraction.County, 80, request.BuyerCounty),
            request.BuyerRadiusKm, ParsePreferredDeliveryDate(extraction.PreferredDeliveryDate), quotationDeadline,
            extraction.AlternativeProductsAccepted, extraction.AllowSplit, extraction.DeliveryRequired,
            extraction.EstimatedBudget is > 0 ? extraction.EstimatedBudget : null, null, lines);
        return new(Clean(extraction.ProjectSummary, 2000, "Proiect de construcții în lucru."), draft,
            extraction.Assumptions.Where(NotBlank).Take(25).ToArray(),
            extraction.Uncertainties.Where(NotBlank).Take(25).ToArray(), clarification,
            transport.ProviderName, transport.Model, transport.PromptVersion, inputTokens, outputTokens);
    }

    private static IntakeCategory? Resolve(string? slug, Dictionary<string, IntakeCategory> categories) =>
        slug is not null && categories.TryGetValue(slug, out IntakeCategory? value) ? value : null;
    private static string Clean(string? value, int length, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, length)];
    private static string? CleanNullable(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Clean(value, length, string.Empty);
    private static bool NotBlank(string value) => !string.IsNullOrWhiteSpace(value);

    private static DateOnly? ParsePreferredDeliveryDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateOnly date) ? date : null;

    private static DateTimeOffset ParseQuotationDeadline(string? value, DateTimeOffset now) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out DateTimeOffset deadline) && deadline > now
            ? deadline : now.AddDays(3);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonNode Schema = JsonNode.Parse("""
    {
      "type":"object","additionalProperties":false,
      "required":["projectSummary","title","projectName","description","primaryCategorySlug","deliveryAddress","deliveryLocality","county","preferredDeliveryDate","quotationDeadline","alternativeProductsAccepted","allowSplit","deliveryRequired","estimatedBudget","lines","assumptions","uncertainties","clarification"],
      "properties":{
        "projectSummary":{"type":"string"},
        "title":{"type":"string"},"projectName":{"type":["string","null"]},"description":{"type":["string","null"]},
        "primaryCategorySlug":{"type":["string","null"]},"deliveryAddress":{"type":"string"},"deliveryLocality":{"type":"string"},"county":{"type":"string"},
        "preferredDeliveryDate":{"type":["string","null"]},"quotationDeadline":{"type":"string"},
        "alternativeProductsAccepted":{"type":"boolean"},"allowSplit":{"type":"boolean"},"deliveryRequired":{"type":"boolean"},
        "estimatedBudget":{"type":["number","null"]},
        "lines":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["kind","name","specification","quantity","unit","alternativesAccepted","categorySlug"],"properties":{"kind":{"type":"string","enum":["Product","Service"]},"name":{"type":"string"},"specification":{"type":["string","null"]},"quantity":{"type":["number","null"]},"unit":{"anyOf":[{"type":"null"},{"type":"string","enum":["Piece","Pack","Bag","Kilogram","Tonne","Metre","SquareMetre","CubicMetre","Litre","Pallet","Other"]}]},"alternativesAccepted":{"type":"boolean"},"categorySlug":{"type":["string","null"]}}}},
        "assumptions":{"type":"array","items":{"type":"string"}},"uncertainties":{"type":"array","items":{"type":"string"}},
        "clarification":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"required":["field","question","choices"],"properties":{"field":{"type":"string","enum":["Category","Quantity","Specification","ProjectScope","DeliveryAddress","DeliveryDate"]},"question":{"type":"string"},"choices":{"type":"array","items":{"type":"string"}}}}]}
      }
    }
    """)!;

    private sealed record ProjectExtraction(string ProjectSummary, string Title, string? ProjectName, string? Description,
        string? PrimaryCategorySlug, string DeliveryAddress, string DeliveryLocality, string County,
        string? PreferredDeliveryDate, string? QuotationDeadline, bool AlternativeProductsAccepted,
        bool AllowSplit, bool DeliveryRequired, decimal? EstimatedBudget,
        IReadOnlyCollection<ProjectLine> Lines, IReadOnlyCollection<string> Assumptions,
        IReadOnlyCollection<string> Uncertainties, ProjectClarification? Clarification);
    private sealed record ProjectLine(string Kind, string Name, string? Specification, decimal? Quantity,
        string? Unit, bool AlternativesAccepted, string? CategorySlug);
    private sealed record ProjectClarification(string Field, string Question, IReadOnlyCollection<string> Choices);
}
