using Marketplace.Domain.Profiles;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class OpportunityMatchConfiguration : IEntityTypeConfiguration<OpportunityMatch>
{
    public void Configure(EntityTypeBuilder<OpportunityMatch> builder)
    {
        builder.ToTable("OpportunityMatches", table => table.HasCheckConstraint("CK_OpportunityMatches_ExactlyOneTarget", "(\"SupplierProfileId\" IS NOT NULL) <> (\"SupplierCandidateId\" IS NOT NULL)"));
        builder.HasKey(match => match.Id);
        builder.Property(match => match.MatchReason).HasMaxLength(1000).IsRequired();
        builder.Property(match => match.AdminReviewNote).HasMaxLength(1000);
        builder.Property(match => match.Version).IsRowVersion();
        builder.HasIndex(match => new { match.RfqId, match.SupplierProfileId }).IsUnique().HasFilter("\"SupplierProfileId\" IS NOT NULL");
        builder.HasIndex(match => new { match.RfqId, match.SupplierCandidateId }).IsUnique().HasFilter("\"SupplierCandidateId\" IS NOT NULL");
        builder.HasIndex(match => new { match.SupplierCandidateId, match.Status, match.MatchedAt });
        builder.HasIndex(match => new { match.RfqId, match.Status });
        builder.HasOne<Rfq>().WithMany().HasForeignKey(match => match.RfqId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierProfile>().WithMany().HasForeignKey(match => match.SupplierProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierCandidate>().WithMany().HasForeignKey(match => match.SupplierCandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(match => match.AdminReviewedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
