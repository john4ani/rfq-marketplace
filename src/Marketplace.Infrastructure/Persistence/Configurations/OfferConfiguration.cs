using Marketplace.Domain.Offers;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Rfqs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("Offers", table => {
            table.HasCheckConstraint("CK_Offers_DeliveryCost", "\"DeliveryCost\" >= 0");
            table.HasCheckConstraint("CK_Offers_DeliveryDays", "\"DeliveryDays\" BETWEEN 0 AND 365");
        });
        builder.HasKey(offer => offer.Id);
        builder.Property(offer => offer.Currency).HasMaxLength(3).IsRequired();
        builder.Property(offer => offer.PricingMode).HasDefaultValue(OfferPricingMode.Itemized);
        builder.Property(offer => offer.TotalPrice).HasPrecision(18, 2);
        builder.Property(offer => offer.DeliveryCost).HasPrecision(18, 2);
        builder.Property(offer => offer.PaymentTerms).HasMaxLength(500);
        builder.Property(offer => offer.Notes).HasMaxLength(2000);
        builder.Ignore(offer => offer.ItemsTotal);
        builder.Ignore(offer => offer.Total);
        builder.HasIndex(offer => new { offer.RfqId, offer.SupplierId }).IsUnique();
        builder.HasIndex(offer => new { offer.SupplierId, offer.Status });
        builder.HasOne<Rfq>().WithMany().HasForeignKey(offer => offer.RfqId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SupplierProfile>().WithMany().HasForeignKey(offer => offer.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(offer => offer.Items).WithOne().HasForeignKey(item => item.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(offer => offer.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
