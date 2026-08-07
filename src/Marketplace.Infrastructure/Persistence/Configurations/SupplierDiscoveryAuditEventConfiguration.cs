using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierDiscoveryAuditEventConfiguration : IEntityTypeConfiguration<SupplierDiscoveryAuditEvent>
{
    public void Configure(EntityTypeBuilder<SupplierDiscoveryAuditEvent> builder)
    {
        builder.ToTable("SupplierDiscoveryAuditEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Details).HasMaxLength(1000);
        builder.HasIndex(item => new { item.CandidateId, item.OccurredAt });
        builder.HasOne<SupplierCandidate>().WithMany().HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SupplierInvitation>().WithMany().HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Rfq>().WithMany().HasForeignKey(item => item.RfqId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
