using Marketplace.Infrastructure.SupplierDiscovery;

namespace Marketplace.IntegrationTests;

public sealed class SupplierDiscoveryPolicyTests
{
    private readonly CategoryDiscoveryProfileProvider profiles = new();

    [Fact]
    public void AutoPartsProfileRetainsGenericGoogleRecallButRequiresWebConfirmation()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Piese auto", ["Plăcuțe de frână"]);

        Assert.Contains("auto_parts_store", profile.SupportingGoogleTypes);
        Assert.Contains("doar recall", profile.QualificationGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Produsul concret", profile.QualificationGuidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TimberProfilePrioritizesRawTimberAndTreatsSawmillingAsSecondary()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Lemn și produse din lemn", ["Lemn rotund de rășinoase pentru construcții"]);

        Assert.Contains("CAEN 0220", profile.PrimaryActivityHints);
        Assert.Contains("CAEN 1610", profile.SecondaryActivityHints);
        Assert.Contains("transport", profile.FalsePositiveHints);
        Assert.Contains("nu confirmă furnizarea de bușteni", profile.QualificationGuidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCategoryUsesSafeProductBasedFallback()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Echipamente industriale", ["Alternator industrial"]);

        Assert.Contains("Echipamente industriale", profile.SearchTerms);
        Assert.Contains("Alternator industrial", profile.SearchTerms);
        Assert.Contains("nu deduce relevanța doar din localitate", profile.QualificationGuidance, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvidencePipelineUsesBothCollectorsAndPreservesGeographicProvenance()
    {
        FakeGoogleCollector google = new(new([new(new("place-1", "Carlio Grup - Piese Auto", null, "Marginea", "Suceava", 47.8, 25.8,
            "shopping_mall", null, ["shopping_mall"], null, "https://maps.example/carlio"), 1m)], []));
        FakeWebCollector web = new(new(null, []));
        SupplierEvidencePipeline pipeline = new(google, web, new SupplierEvidenceMerger());
        SupplierDiscoveryEvidenceRequest request = new("Piese auto", [], "Marginea", "Suceava", 47.81m, 25.81m, 30,
            ["Plăcuțe de frână"], "piese auto Marginea", "prompt", "Google este doar recall.");

        SupplierEvidencePipelineResult result = await pipeline.CollectAsync(request, CancellationToken.None);

        Assert.True(google.WasCalled);
        Assert.True(web.WasCalled);
        Assert.Equal("Carlio Grup - Piese Auto", result.Evidence["mergedEvidence"]![0]!["companyName"]!.GetValue<string>());
        Assert.Equal(1m, result.Evidence["mergedEvidence"]![0]!["distanceKm"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task GoogleCollectorUsesRfqCoordinatesAndRadius()
    {
        FakeGooglePlacesClient client = new();
        GooglePlacesEvidenceCollector collector = new(client);
        SupplierDiscoveryEvidenceRequest request = new("Piese auto", [], "Marginea", "Suceava", 47.81m, 25.81m, 30,
            ["Plăcuțe de frână"], "piese auto Marginea", "prompt", "Google este doar recall.");

        GoogleEvidenceResult result = await collector.CollectAsync(request, CancellationToken.None);

        Assert.Equal(["nearby"], client.Calls);
        Assert.All(result.Places, place => Assert.InRange(place.DistanceKm, 0m, 30m));
        Assert.DoesNotContain(result.Places, place => place.Place.DisplayName == "Far Auto");
    }

    private sealed class FakeGoogleCollector(GoogleEvidenceResult result) : IGooglePlacesEvidenceCollector
    {
        public bool WasCalled { get; private set; }
        public Task<GoogleEvidenceResult> CollectAsync(SupplierDiscoveryEvidenceRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeWebCollector(WebEvidenceResult result) : IWebEvidenceCollector
    {
        public bool WasCalled { get; private set; }
        public Task<WebEvidenceResult> CollectAsync(SupplierDiscoveryEvidenceRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeGooglePlacesClient : IGooglePlacesClient
    {
        public bool IsAvailable => true;
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<GooglePlaceEvidence>> SearchTextAsync(string textQuery, string regionCode, CancellationToken cancellationToken)
        {
            Calls.Add(textQuery.Contains("Rădăuți", StringComparison.OrdinalIgnoreCase)
                ? "nearby"
                : textQuery.Contains("Suceava", StringComparison.OrdinalIgnoreCase) && !textQuery.Contains("Marginea", StringComparison.OrdinalIgnoreCase)
                    ? "county" : "text");
            return Task.FromResult<IReadOnlyList<GooglePlaceEvidence>>([Place("Carlio Grup - Piese Auto", "Marginea", 47.81, 25.81)]);
        }

        public Task<IReadOnlyList<GooglePlaceEvidence>> SearchNearbyAsync(decimal latitude, decimal longitude, int radiusKm, CancellationToken cancellationToken)
        {
            Calls.Add("nearby");
            return Task.FromResult<IReadOnlyList<GooglePlaceEvidence>>([
                Place("Auto One", "Rădăuți", 47.84, 25.92), Place("Auto Two", "Rădăuți", 47.85, 25.93),
                Place("Auto Three", "Rădăuți", 47.86, 25.94), Place("Auto Four", "Rădăuți", 47.87, 25.95),
                Place("Far Auto", "Suceava", 47.81, 26.30)]);
        }

        private static GooglePlaceEvidence Place(string name, string locality, double latitude, double longitude) => new(
            name, name, null, locality, "Suceava", latitude, longitude, "auto_parts_store", null, ["auto_parts_store"], null, null);
    }
}
