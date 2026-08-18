using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierInvitationConfiguration : IEntityTypeConfiguration<SupplierInvitation>
{
    public void Configure(EntityTypeBuilder<SupplierInvitation> builder)
    {
        builder.ToTable("SupplierInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(invitation => invitation.Version).IsRowVersion();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.CandidateId, invitation.RfqId });
        builder.HasIndex(invitation => invitation.OpportunityMatchId);
        builder.HasOne<SupplierCandidate>().WithMany().HasForeignKey(invitation => invitation.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Rfq>().WithMany().HasForeignKey(invitation => invitation.RfqId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OpportunityMatch>().WithMany().HasForeignKey(invitation => invitation.OpportunityMatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invitation => invitation.ClaimedBySupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
