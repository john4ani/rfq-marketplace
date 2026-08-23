namespace Marketplace.Infrastructure.Bidding;

/// <summary>Fixed, predictable LLM output for deterministic tests in the CI/CD pipeline.</summary>
public sealed class StaticMockLlmProvider(string jsonResponse) : ILlmProvider
{
    public static string RomanianProcurementExpertProposal =>
        """
        {
          "title": "Ofertă tehnică — Lucrări de construcții",
          "technicalProposal": {
            "sections": [
              { "heading": "Înțelegerea cerințelor", "content": "Firma își asumă toate cerințele din caietul de sarcini." },
              { "heading": "Metodologie", "content": "Lucrările se execută conform normelor în vigoare." }
            ]
          },
          "billOfQuantities": [
            { "position": 1, "description": "Transport materiale", "unit": "buc", "quantity": 2, "unitPrice": 250, "total": 500 }
          ]
        }
        """;

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
        Task.FromResult(jsonResponse);
}
