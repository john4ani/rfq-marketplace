namespace Marketplace.Domain.Seap;

/// <summary>
/// Single, centralized readiness rule for the Direct Hit flow. A tender is served as ready (HTTP 200 with
/// the full detail DTO) only when the stored analysis is document-grounded, was produced from the CURRENT
/// document set (<see cref="SeapTender.AnalyzedDocumentSetHash"/> == <see cref="SeapTender.DocumentSetHash"/>),
/// uses a valid <see cref="SeapTender.AnalysisVersion"/> and is not being rebuilt by an active ingestion run
/// (the no-active-run check is made by the caller, which owns the run query).
/// </summary>
public static class SeapTenderReadiness
{
    public static bool IsDocumentAnalysisReady(SeapTender tender) =>
        tender.SummaryGroundedInDocuments
        && !string.IsNullOrWhiteSpace(tender.AnalyzedDocumentSetHash)
        && string.Equals(tender.AnalyzedDocumentSetHash, tender.DocumentSetHash, StringComparison.Ordinal)
        && tender.AnalysisVersion is > 0;
}
