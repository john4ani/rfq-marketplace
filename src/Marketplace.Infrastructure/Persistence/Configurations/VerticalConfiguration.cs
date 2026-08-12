using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class VerticalConfiguration : IEntityTypeConfiguration<Vertical>
{
    public static readonly Guid ConstructionId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    // Compatibility storage for the generic entry mode. It deliberately carries no domain or matching rules.
    public static readonly Guid GeneralProcurementCompatibilityId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<Vertical> builder)
    {
        builder.ToTable("Verticals");
        builder.HasKey(vertical => vertical.Id);
        builder.Property(vertical => vertical.Name).HasMaxLength(120).IsRequired();
        builder.Property(vertical => vertical.Slug).HasMaxLength(120).IsRequired();
        builder.HasIndex(vertical => vertical.Slug).IsUnique();
        builder.HasData(
            Vertical.Create(ConstructionId, "Construcții", "constructii"),
            Vertical.Create(GeneralProcurementCompatibilityId, "Achiziții generale (provizoriu)", "achizitii-generale-provizoriu"));
    }
}
