using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierDeliverySettingsConfiguration : IEntityTypeConfiguration<SupplierDeliverySettings>
{
    public void Configure(EntityTypeBuilder<SupplierDeliverySettings> builder)
    {
        builder.ToTable("SupplierDeliverySettings", table =>
        {
            table.HasCheckConstraint("CK_SupplierDeliverySettings_NormalRadius", "\"NormalDeliveryRadiusKm\" BETWEEN 1 AND 1000");
            table.HasCheckConstraint("CK_SupplierDeliverySettings_MaximumRadius", "\"MaximumDeliveryRadiusKm\" BETWEEN \"NormalDeliveryRadiusKm\" AND 1000");
            table.HasCheckConstraint("CK_SupplierDeliverySettings_MinimumOrder", "\"MinimumOrderValue\" IS NULL OR \"MinimumOrderValue\" >= 0");
        });
        builder.HasKey(settings => settings.SupplierId);
        builder.Property(settings => settings.MinimumOrderValue).HasPrecision(18, 2);
        builder.Property(settings => settings.CanFulfillMultiCategoryRequests).HasDefaultValue(false);
        builder.HasOne<SupplierProfile>().WithOne().HasForeignKey<SupplierDeliverySettings>(settings => settings.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}
