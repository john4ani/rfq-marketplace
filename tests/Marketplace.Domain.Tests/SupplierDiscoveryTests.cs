using Marketplace.Domain.SupplierDiscovery;

namespace Marketplace.Domain.Tests;

public sealed class SupplierDiscoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CandidateRequiresHumanValidationBeforeClaim()
    {
        SupplierCandidate candidate = Candidate();

        Assert.Throws<SupplierDiscoveryRuleException>(() => candidate.Claim(Guid.NewGuid(), Now.AddMinutes(1)));

        Guid administratorId = Guid.NewGuid();
        Guid supplierId = Guid.NewGuid();
        candidate.ValidateRelevance(administratorId, "Categoria și zona corespund cererii.", Now.AddMinutes(2));
        candidate.Claim(supplierId, Now.AddMinutes(3));

        Assert.Equal(SupplierCandidateStatus.Claimed, candidate.Status);
        Assert.Equal(supplierId, candidate.ClaimedBySupplierId);
    }

    [Fact]
    public void InvitationCannotBeClaimedAfterExpiry()
    {
        SupplierInvitation invitation = SupplierInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HASH", Guid.NewGuid(), Now, Now.AddDays(14));

        Assert.Equal(SupplierInvitationStatus.Expired, invitation.StatusAt(Now.AddDays(15)));
        Assert.Throws<SupplierDiscoveryRuleException>(() =>
            invitation.Claim(Guid.NewGuid(), Now.AddDays(15)));
    }

    [Fact]
    public void CandidateRejectionRequiresReasonAndIsTerminal()
    {
        SupplierCandidate candidate = Candidate();

        Assert.Throws<SupplierDiscoveryRuleException>(() =>
            candidate.Reject(Guid.NewGuid(), " ", Now.AddMinutes(1)));
        candidate.Reject(Guid.NewGuid(), "Datele nu indică o activitate relevantă.", Now.AddMinutes(2));

        Assert.Equal(SupplierCandidateStatus.Rejected, candidate.Status);
        Assert.Throws<SupplierDiscoveryRuleException>(() =>
            candidate.ValidateRelevance(Guid.NewGuid(), "Reverificare", Now.AddMinutes(3)));
    }

    [Fact]
    public void QualificationFailureKeepsWebEvidenceAvailableForRetry()
    {
        SupplierWebDiscoveryRun run = SupplierWebDiscoveryRun.Start(Guid.NewGuid(), Guid.NewGuid(), Now);
        run.RecordEvidence("{\"evidence\":[]}", 2, 1_200, 20, 10, Now.AddMinutes(1));

        run.RecordQualificationFailure("Calificarea nu a reușit.", Now.AddMinutes(2));

        Assert.Equal(SupplierWebDiscoveryRunStatus.EvidenceReady, run.Status);
        Assert.Equal("{\"evidence\":[]}", run.EvidenceJson);
        Assert.Equal("Calificarea nu a reușit.", run.FailureMessage);
    }

    [Fact]
    public void CompanyEvidenceAliasesTreatMardezAsOneCompany()
    {
        Assert.True(CompanyEvidenceIdentity.IsCompatibleEvidence(
            "MARDEZ SRL",
            "Marginea", "Suceava",
            "mardez srl piese auto din dezmembrari",
            null, "Suceava"));
        Assert.False(CompanyEvidenceIdentity.IsCompatibleEvidence(
            "MARDEZ SRL", "Marginea", "Suceava",
            "mardez srl piese auto din dezmembrari", "Botoșani", "Botoșani"));
    }

    private static SupplierCandidate Candidate() => SupplierCandidate.Create(
        Guid.NewGuid(),
        "Lemn Local SRL",
        "contact@example.test",
        null,
        "https://example.test",
        "Marginea",
        "Suceava",
        [Guid.NewGuid()],
        "Registru public consultat manual",
        null,
        Guid.NewGuid(),
        Now);
}
