using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierDeliveryAreaConfiguration : IEntityTypeConfiguration<SupplierDeliveryArea>
{
    public void Configure(EntityTypeBuilder<SupplierDeliveryArea> builder)
    {
        builder.ToTable("SupplierDeliveryAreas");
        builder.HasKey(area => new { area.SupplierId, area.County });
        builder.Property(area => area.County).HasMaxLength(80);
        builder.HasOne<SupplierProfile>().WithMany().HasForeignKey(area => area.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}
