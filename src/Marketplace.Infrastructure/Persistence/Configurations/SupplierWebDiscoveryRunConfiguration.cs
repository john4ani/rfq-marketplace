using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierWebDiscoveryRunConfiguration : IEntityTypeConfiguration<SupplierWebDiscoveryRun>
{
    public void Configure(EntityTypeBuilder<SupplierWebDiscoveryRun> builder)
    {
        builder.ToTable("SupplierWebDiscoveryRuns");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EvidenceJson).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.QualificationJson).HasColumnType("jsonb");
        builder.Property(item => item.FailureMessage).HasMaxLength(500);
        builder.HasIndex(item => new { item.RfqId, item.StartedAt });
        builder.HasOne<Rfq>().WithMany().HasForeignKey(item => item.RfqId).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.StartedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
