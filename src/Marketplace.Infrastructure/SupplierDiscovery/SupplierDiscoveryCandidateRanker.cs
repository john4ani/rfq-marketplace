using Marketplace.Application.SupplierDiscovery;
using Marketplace.Domain.SupplierDiscovery;

namespace Marketplace.Infrastructure.SupplierDiscovery;

// The model decides whether public evidence is commercially plausible. This class owns the
// repeatable final ordering and never uses locality, county, or AI array order as a rank signal.
public static class SupplierDiscoveryCandidateRanker
{
    private const int MaximumFinalCandidates = 10;

    public static SupplierDiscoveryFinalRanking Rank(
        IReadOnlyCollection<SupplierWebCandidateView> qualified,
        IReadOnlyCollection<SupplierWebEvidenceView> evidence,
        CategoryDiscoveryProfile profile)
    {
        List<RankedCandidate> eligible = [];
        List<SupplierWebRejectedResult> rejected = [];

        foreach (SupplierWebCandidateView candidate in qualified)
        {
            // Evidence reaching this ranker was already geocoded and radius-validated.
            // Textual locality formatting must not invalidate those persisted coordinates.
            SupplierWebEvidenceView? matched = evidence.FirstOrDefault(item => CompanyEvidenceIdentity.AreSame(
                candidate.CompanyName, item.CompanyName));
            if (matched?.Latitude is null || matched.Longitude is null || matched.DistanceKm is null)
            {
                rejected.Add(new(candidate.CompanyName, "Nu există coordonate utilizabile, în raza cererii, pentru această firmă.", candidate.SourceUrls));
                continue;
            }

            CandidateScores scores = Score(candidate, matched, profile);
            if (scores.CategoryRelevance == 0)
            {
                rejected.Add(new(candidate.CompanyName, "Dovezile nu confirmă relevanța pentru categoria principală a cererii.", candidate.SourceUrls.Concat(matched.SourceUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
                continue;
            }

            SupplierWebCandidateView enriched = candidate with
            {
                DistanceKm = matched.DistanceKm,
                Sources = matched.Sources,
                SourceUrls = candidate.SourceUrls.Concat(matched.SourceUrls).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            eligible.Add(new(enriched, scores));
        }

        return new(
            eligible
                .OrderByDescending(item => item.Scores.CategoryRelevance)
                .ThenBy(item => item.Candidate.DistanceKm)
                .ThenByDescending(item => item.Scores.ProductEvidence)
                .ThenByDescending(item => item.Scores.ActivityEvidence)
                .ThenByDescending(item => item.Scores.SourceQuality)
                .ThenBy(item => item.Candidate.CompanyName, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumFinalCandidates)
                .Select(item => item.Candidate)
                .ToArray(),
            rejected);
    }

    private static CandidateScores Score(
        SupplierWebCandidateView candidate,
        SupplierWebEvidenceView evidence,
        CategoryDiscoveryProfile profile)
    {
        string commercialText = string.Join(' ', new[]
        {
            candidate.CompanyName, candidate.ProductEvidence, candidate.ActivityEvidence,
            candidate.CaenCode, candidate.WhyRelevant, string.Join(' ', candidate.Sources), string.Join(' ', evidence.Sources)
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        int primaryHits = Hits(commercialText, profile.PrimaryActivityHints);
        int recallHits = Hits(commercialText, profile.SearchTerms);
        int productHits = Hits(commercialText, profile.SearchTerms);
        int secondaryHits = Hits(commercialText, profile.SecondaryActivityHints);
        bool freightOrOtherFalsePositiveWithoutPrimaryEvidence = Hits(commercialText, profile.FalsePositiveHints) > 0 && primaryHits == 0;
        int categoryRelevance = freightOrOtherFalsePositiveWithoutPrimaryEvidence ? 0 : (primaryHits * 100) + (recallHits * 80) + (secondaryHits * 20);
        return new(categoryRelevance, productHits * 40, (primaryHits * 30) + (secondaryHits * 10), SourceQuality(candidate, evidence));
    }

    private static int Hits(string text, IEnumerable<string> terms) => terms
        .Where(term => !string.IsNullOrWhiteSpace(term))
        .Count(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static int SourceQuality(SupplierWebCandidateView candidate, SupplierWebEvidenceView evidence) =>
        candidate.SourceUrls.Count + evidence.SourceUrls.Count +
        (candidate.Sources.Contains("Google Places", StringComparer.Ordinal) ? 1 : 0) +
        (candidate.Sources.Contains("OpenAI Web Search", StringComparer.Ordinal) ? 2 : 0);

    private sealed record RankedCandidate(SupplierWebCandidateView Candidate, CandidateScores Scores);
    private sealed record CandidateScores(int CategoryRelevance, int ProductEvidence, int ActivityEvidence, int SourceQuality);
}

public sealed record SupplierDiscoveryFinalRanking(
    IReadOnlyCollection<SupplierWebCandidateView> Candidates,
    IReadOnlyCollection<SupplierWebRejectedResult> RejectedResults);
