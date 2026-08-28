using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Application.AiIntake;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;

namespace Marketplace.Infrastructure.AiIntake;

// SEAP Tender Match interpreter: a third distinct interpretation workflow (like General and Construction)
// that translates a free-text description into rigid SEAP filter parameters — CPV prefixes and counties.
// It reuses the shared OpenAI Responses transport and structured output (ADR-002 §14), never the general
// prompt/schema. County codes are normalized deterministically to the canonical county names the database
// stores, so the LLM can never invent a county that the Anunțuri SEAP filter would not match.
internal sealed class OpenAiSeapTenderMatchInterpreter(OpenAiResponsesTransport transport) : ISeapTenderMatchInterpreter
{
    public bool IsAvailable => transport.IsAvailable;

    public async Task<SeapTenderMatchResult> InterpretAsync(
        SeapTenderMatchRequest request, CancellationToken cancellationToken)
    {
        OpenAiStructuredResponse response = await transport.SendAsync(BuildRequest(request), cancellationToken);
        MatchExtraction extraction = JsonSerializer.Deserialize<MatchExtraction>(response.OutputText, JsonOptions)
            ?? throw new InvalidOperationException("AI SEAP tender match structured output is empty.");
        return Map(request, extraction, response.InputTokens, response.OutputTokens);
    }

    private static OpenAiStructuredRequest BuildRequest(SeapTenderMatchRequest request)
    {
        string turns = request.Turns.Count == 0 ? "Nicio clarificare încă." : string.Join("\n",
            request.Turns.Select(turn => $"Î: {turn.Question}\nR: {turn.Answer}"));
        string instructions = """
            Ești asistentul CompaSpot pentru licitații publice (SEAP). Ajuți furnizorii să-și configureze
            potrivirea de anunțuri: traduci o descriere în limbaj natural în parametri rigizi — prefixe CPV și
            județe — folosiți pentru filtrarea licitațiilor din baza de date.

            1. Extrage prefixele CPV (8 cifre) pentru domeniile de achiziție descrise. Exemple de orientare:
            lucrări de drumuri → 45233000, poduri → 45221111, construcții de clădiri → 45210000, medicamente →
            33600000, echipamente medicale → 33100000, mobilier → 39100000, hardware/IT → 30200000, servicii de
            curățenie → 90910000, transport public → 60112000, produse alimentare → 15000000. Nu inventa
            prefixe CPV pe care nu le poți susține din descriere.
            2. Extrage județele/zona geografică dorită și pune codurile de județ (IS, SV, BT, CJ, B, ...).
            O zonă precum „Moldova" acoperă IS, SV, BT, BC, VS, NT, GL, VR; „Transilvania" acoperă CJ, BN, AB,
            SB, MS, HR, CV, BH, MM, SM, SJ; „Oltenia" acoperă DJ, GJ, MH, OT, VL, TR. Dacă utilizatorul nu
            menționează o zonă sau spune „toată țara", lasă lista goală.
            3. Dacă descrierea este prea vagă pentru a extrage cu încredere cel puțin un prefix CPV, pune
            clarificationNeeded=true și exact o întrebare scurtă de rafinare în clarificationQuestion. Pune
            exact o singură întrebare pe rând.
            4. summaryText rezumă în limbaj natural, pe scurt, ce ai înțeles (de exemplu „Am identificat
            interes pentru Lucrări de drumuri și poduri în zona Moldovei.").

            Scrie toate textele în română.
            """;
        string input = $"""
            Descriere inițială:
            {request.InitialInput}

            Clarificări anterioare:
            {turns}
            """;
        return new OpenAiStructuredRequest(instructions, input, "seap_tender_match", Schema, DeterministicSeed(request.InitialInput));
    }

    private SeapTenderMatchResult Map(SeapTenderMatchRequest request, MatchExtraction extraction, int inputTokens, int outputTokens)
    {
        string[] cpvPrefixes = extraction.ExtractedCpvPrefixes
            .Select(CpvPrefix.TryNormalize).Where(prefix => prefix is not null).Select(prefix => prefix!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] countyCodes = extraction.ExtractedCountyCodes
            .Where(code => RomanianCountyCodes.TryNormalize(code) is not null)
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] counties = countyCodes
            .Select(RomanianCountyCodes.TryNormalize).Where(county => county is not null).Select(county => county!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(extraction.ClarificationNeeded,
            CleanNullable(extraction.ClarificationQuestion, 300),
            cpvPrefixes, countyCodes, counties,
            CleanNullable(extraction.SummaryText, 500),
            transport.ProviderName, transport.Model, transport.PromptVersion, inputTokens, outputTokens);
    }

    private static string? CleanNullable(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];

    /// <summary>Deterministic safety identifier seed: the same description always yields the same seed.</summary>
    private static Guid DeterministicSeed(string input) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(input))[..16]);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonNode Schema = JsonNode.Parse("""
    {
      "type":"object","additionalProperties":false,
      "required":["clarificationNeeded","clarificationQuestion","extractedCpvPrefixes","extractedCountyCodes","summaryText"],
      "properties":{
        "clarificationNeeded":{"type":"boolean"},
        "clarificationQuestion":{"type":["string","null"]},
        "extractedCpvPrefixes":{"type":"array","items":{"type":"string"}},
        "extractedCountyCodes":{"type":"array","items":{"type":"string"}},
        "summaryText":{"type":["string","null"]}
      }
    }
    """)!;

    private sealed record MatchExtraction(
        bool ClarificationNeeded,
        string? ClarificationQuestion,
        IReadOnlyCollection<string> ExtractedCpvPrefixes,
        IReadOnlyCollection<string> ExtractedCountyCodes,
        string? SummaryText);
}
