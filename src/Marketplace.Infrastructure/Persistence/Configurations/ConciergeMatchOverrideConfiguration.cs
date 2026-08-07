using Marketplace.Domain.Operations;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class ConciergeMatchOverrideConfiguration : IEntityTypeConfiguration<ConciergeMatchOverride>
{
    public void Configure(EntityTypeBuilder<ConciergeMatchOverride> builder)
    {
        builder.ToTable("ConciergeMatchOverrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(item => new { item.RfqId, item.SupplierId }).IsUnique();
        builder.HasOne<Rfq>().WithMany().HasForeignKey(item => item.RfqId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SupplierProfile>().WithMany().HasForeignKey(item => item.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AdministratorId).OnDelete(DeleteBehavior.Restrict);
    }
}
