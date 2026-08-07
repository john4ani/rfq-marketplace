namespace Marketplace.Domain.SupplierDiscovery;

public sealed class SupplierCandidateCategory
{
    private SupplierCandidateCategory() { }

    public Guid CandidateId { get; private set; }
    public Guid CategoryId { get; private set; }

    internal static SupplierCandidateCategory Create(Guid candidateId, Guid categoryId)
    {
        if (candidateId == Guid.Empty || categoryId == Guid.Empty)
            throw new SupplierDiscoveryRuleException("Selectează cel puțin o categorie validă pentru firmă.");
        return new SupplierCandidateCategory { CandidateId = candidateId, CategoryId = categoryId };
    }
}
