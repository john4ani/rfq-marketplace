using Marketplace.Domain.Offers;
using Marketplace.Domain.Rfqs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class OfferItemConfiguration : IEntityTypeConfiguration<OfferItem>
{
    public void Configure(EntityTypeBuilder<OfferItem> builder)
    {
        builder.ToTable("OfferItems", table => {
            table.HasCheckConstraint("CK_OfferItems_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_OfferItems_UnitPrice", "\"UnitPrice\" >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.ProductDescription).HasMaxLength(500);
        builder.Property(item => item.IsAlternative).HasDefaultValue(false);
        builder.HasIndex(item => new { item.OfferId, item.RfqItemId }).IsUnique();
        builder.HasOne<RfqItem>().WithMany().HasForeignKey(item => item.RfqItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
