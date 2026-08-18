using Marketplace.Domain.SupplierDiscovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierContactSuppressionConfiguration : IEntityTypeConfiguration<SupplierContactSuppression>
{
    public void Configure(EntityTypeBuilder<SupplierContactSuppression> builder)
    {
        builder.ToTable("SupplierContactSuppressions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NormalizedEmail).HasMaxLength(254);
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.Property(item => item.Version).IsRowVersion();
        builder.HasIndex(item => new { item.CandidateId, item.Scope }).IsUnique();
        builder.HasIndex(item => new { item.NormalizedEmail, item.Scope });
        builder.HasOne<SupplierCandidate>().WithMany().HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Cascade);
    }
}
