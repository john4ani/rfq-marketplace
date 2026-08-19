using Marketplace.Domain.Rfqs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class RfqItemConfiguration : IEntityTypeConfiguration<RfqItem>
{
    public void Configure(EntityTypeBuilder<RfqItem> builder)
    {
        builder.ToTable("RfqItems", table => table.HasCheckConstraint("CK_RfqItems_Quantity", "\"Quantity\" > 0"));
        builder.HasKey(item => item.Id);
        builder.Property<Guid>("_needCategoryId").HasColumnName("NeedCategoryId");
        builder.HasIndex("_needCategoryId");
        builder.HasOne<NeedCategory>().WithMany().HasForeignKey("_needCategoryId").OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.ProductName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Kind).HasDefaultValue(RequirementKind.Product);
        builder.Property(item => item.ManufacturerOrBrand).HasMaxLength(160);
        builder.Property(item => item.Specification).HasMaxLength(1000);
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.BuyerNotes).HasMaxLength(1000);
        builder.HasIndex(item => new { item.RfqId, item.ItemNumber }).IsUnique();
    }
}
