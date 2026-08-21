using Marketplace.Application.SupplierDiscovery;
using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
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
        SupplierEvidencePipeline pipeline = new(google, web, new SupplierEvidenceMerger(new FakeGeocoder()));
        SupplierDiscoveryEvidenceRequest request = new("Piese auto", [], "Marginea", "Suceava", 47.81m, 25.81m, 30,
            ["Plăcuțe de frână"], ["piese auto"], "piese auto Marginea", "prompt", "Google este doar recall.");

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
            ["Plăcuțe de frână"], ["piese auto"], "piese auto Marginea", "prompt", "Google este doar recall.");

        GoogleEvidenceResult result = await collector.CollectAsync(request, CancellationToken.None);

        Assert.Equal(["text"], client.Calls);
        Assert.Contains(result.Places, place => place.Place.DisplayName == "Carlio Grup - Piese Auto");
        Assert.All(result.Places, place => Assert.InRange(place.DistanceKm, 0m, 30m));
        Assert.DoesNotContain(result.Places, place => place.Place.DisplayName == "Far Auto");
    }

    [Fact]
    public void FinalRankingUsesCategoryThenDistanceAndRejectsWrongCategory()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Piese auto", ["Alternator"]);
        SupplierDiscoveryFinalRanking result = SupplierDiscoveryCandidateRanker.Rank(
        [
            Candidate("Carlio Grup - Piese Auto și STIHL", "Piese auto · alternatoare", "piese auto", "Dovezi pentru alternator"),
            Candidate("Piese Auto Mai Departe", "Piese auto · alternatoare", "piese auto", "Dovezi pentru alternator"),
            Candidate("Biserica Evanghelică", "activitate religioasă", "", "Activitate fără legătură")
        ],
        [
            Evidence("Carlio Grup - Piese Auto și STIHL", 2m),
            Evidence("Piese Auto Mai Departe", 18m),
            Evidence("Biserica Evanghelică", 1m)
        ], profile);

        Assert.Equal(["Carlio Grup - Piese Auto și STIHL", "Piese Auto Mai Departe"], result.Candidates.Select(item => item.CompanyName));
        Assert.Contains(result.RejectedResults, item => item.CompanyName == "Biserica Evanghelică");
    }

    [Fact]
    public void WoodProfileAcceptsRawTimberAndRejectsFreightOnly()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Lemn și produse din lemn", ["Lemn rotund de rășinoase pentru construcții"]);
        SupplierDiscoveryFinalRanking result = SupplierDiscoveryCandidateRanker.Rank(
        [
            Candidate("FORESTEXPLO BUCOVIVA SRL", "lemn rotund de rășinoase", "CAEN 0220 · exploatare forestieră", "Produce bușteni"),
            Candidate("HOLZ TRANSPORT SRL", "transport de bușteni", "transport rutier", "Transportă marfă")
        ],
        [Evidence("FORESTEXPLO BUCOVIVA SRL", 12m), Evidence("HOLZ TRANSPORT SRL", 4m)], profile);

        Assert.Single(result.Candidates);
        Assert.Equal("FORESTEXPLO BUCOVIVA SRL", result.Candidates.Single().CompanyName);
        Assert.Contains(result.RejectedResults, item => item.CompanyName == "HOLZ TRANSPORT SRL");
    }

    [Fact]
    public void FinalRankingCapsTheFinalListAtTen()
    {
        CategoryDiscoveryProfile profile = profiles.Resolve("Piese auto", ["Alternator"]);
        SupplierWebCandidateView[] candidates = Enumerable.Range(1, 12)
            .Select(index => Candidate($"Piese Auto {index}", "alternator", "piese auto", "Piese auto relevante"))
            .ToArray();
        SupplierWebEvidenceView[] evidence = Enumerable.Range(1, 12)
            .Select(index => Evidence($"Piese Auto {index}", index))
            .ToArray();

        SupplierDiscoveryFinalRanking result = SupplierDiscoveryCandidateRanker.Rank(candidates, evidence, profile);

        Assert.Equal(10, result.Candidates.Count);
        Assert.Equal("Piese Auto 1", result.Candidates.First().CompanyName);
    }

    private static SupplierWebCandidateView Candidate(string name, string productEvidence, string activityEvidence, string whyRelevant) => new(
        name, "Marginea", "Suceava", null, null, null, activityEvidence, null, productEvidence, "unknown", "high", [], [], whyRelevant);

    private static SupplierWebEvidenceView Evidence(string name, decimal distanceKm) => new(
        name, "Marginea", "Suceava", ["Google Places", "OpenAI Web Search"], ["https://example.test"], 47.81, 25.81, distanceKm);

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

    private sealed class FakeGeocoder : ILocationGeocoder
    {
        public Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken) => Task.FromResult(new GeographicLocation(input.Address, input.Locality, input.County, input.Country, 47.81m, 25.81m));
    }

    private sealed class FakeGooglePlacesClient : IGooglePlacesClient
    {
        public bool IsAvailable => true;
        public List<string> Calls { get; } = [];

        public Task<IReadOnlyList<GooglePlaceEvidence>> SearchTextAsync(string textQuery, string regionCode, decimal latitude, decimal longitude, int radiusKm, CancellationToken cancellationToken)
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
                Place("Carlio Grup - Piese Auto și STIHL", "Marginea", 47.81, 25.81), Place("Auto Two", "Rădăuți", 47.85, 25.93),
                Place("Auto Three", "Rădăuți", 47.86, 25.94), Place("Auto Four", "Rădăuți", 47.87, 25.95),
                Place("Far Auto", "Suceava", 47.81, 26.30)]);
        }

        private static GooglePlaceEvidence Place(string name, string locality, double latitude, double longitude) => new(
            name, name, null, locality, "Suceava", latitude, longitude, "shopping_mall", "centru comercial", ["shopping_mall"], null, null);
    }
}
