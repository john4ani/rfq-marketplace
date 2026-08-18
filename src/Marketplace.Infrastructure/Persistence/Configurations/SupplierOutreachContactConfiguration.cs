using Marketplace.Domain.SupplierDiscovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierOutreachContactConfiguration : IEntityTypeConfiguration<SupplierOutreachContact>
{
    public void Configure(EntityTypeBuilder<SupplierOutreachContact> builder)
    {
        builder.ToTable("SupplierOutreachContacts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Note).HasMaxLength(1000);
        builder.Property(item => item.FailureReason).HasMaxLength(300);
        builder.Property(item => item.OptOutTokenHash).HasMaxLength(64);
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => new { item.CandidateId, item.CreatedAt });
        builder.HasIndex(item => new { item.OpportunityMatchId, item.Channel });
        builder.HasIndex(item => item.OptOutTokenHash).IsUnique().HasFilter("\"OptOutTokenHash\" IS NOT NULL");
        builder.HasOne<SupplierCandidate>().WithMany().HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OpportunityMatch>().WithMany().HasForeignKey(item => item.OpportunityMatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplierInvitation>().WithMany().HasForeignKey(item => item.InvitationId).OnDelete(DeleteBehavior.SetNull);
    }
}
