namespace Marketplace.Infrastructure.SupplierDiscovery;

// Shared commercial discovery policy. A profile changes search vocabulary and evidence guidance,
// never the discovery algorithm or the shared NeedCategory catalog.
public sealed record CategoryDiscoveryProfile(
    string CategoryName,
    IReadOnlyCollection<string> SearchTerms,
    IReadOnlyCollection<string> SupportingGoogleTypes,
    IReadOnlyCollection<string> PrimaryActivityHints,
    IReadOnlyCollection<string> SecondaryActivityHints,
    IReadOnlyCollection<string> FalsePositiveHints,
    string QualificationGuidance);

public interface ICategoryDiscoveryProfileProvider
{
    CategoryDiscoveryProfile Resolve(string categoryName, IReadOnlyCollection<string> requestedProducts);
}

public sealed class CategoryDiscoveryProfileProvider : ICategoryDiscoveryProfileProvider
{
    public CategoryDiscoveryProfile Resolve(string categoryName, IReadOnlyCollection<string> requestedProducts)
    {
        if (categoryName.Contains("Lemn", StringComparison.OrdinalIgnoreCase))
            return new(categoryName,
                ["lemn rotund", "bușteni", "rășinoase", "molid", "brad"], [],
                ["logging", "exploatare forestieră", "CAEN 0220", "lemn brut"],
                ["CAEN 1610", "gater", "cherestea"],
                ["transport", "tâmplărie", "mobilă"],
                "Pentru lemn rotund, CAEN 0220/logging sau dovezi de lemn brut confirmă potrivirea. CAEN 1610/cherestea singur nu confirmă furnizarea de bușteni; transportul, tâmplăria și magazinele generice de lemn se resping fără dovezi specifice.");
        if (categoryName.Contains("Piese auto", StringComparison.OrdinalIgnoreCase))
            return new(categoryName, ["piese auto", "piese originale", "aftermarket"], ["auto_parts_store"],
                ["piese auto"], [], ["jante", "dezmembrări"],
                "Un tip Google auto_parts_store oferă doar recall. Produsul concret trebuie confirmat prin website sau dovezi web; jantele nu confirmă plăcuțe de frână.");
        string[] terms = new[] { categoryName }.Concat(requestedProducts).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(categoryName, terms, [], [], [], [], "Confirmă produsul sau activitatea solicitată prin dovezi publice; nu deduce relevanța doar din localitate sau tipul Google.");
    }
}
