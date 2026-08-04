using Marketplace.Domain.Profiles;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class RfqConfiguration : IEntityTypeConfiguration<Rfq>
{
    public void Configure(EntityTypeBuilder<Rfq> builder)
    {
        builder.ToTable("Rfqs", table =>
        {
            table.HasCheckConstraint("CK_Rfqs_EstimatedBudget", "\"EstimatedBudget\" IS NULL OR \"EstimatedBudget\" >= 0");
            table.HasCheckConstraint("CK_Rfqs_SupplierSearchRadiusKm", "\"SupplierSearchRadiusKm\" BETWEEN 1 AND 1000");
        });
        builder.HasKey(rfq => rfq.Id);
        builder.Property<uint>("xmin").IsRowVersion();
        builder.Property(rfq => rfq.Title).HasMaxLength(200).IsRequired();
        builder.Property(rfq => rfq.ProjectName).HasMaxLength(200);
        builder.Property(rfq => rfq.Description).HasMaxLength(4000);
        builder.Property(rfq => rfq.DeliveryAddress).HasMaxLength(300).IsRequired();
        builder.Property(rfq => rfq.DeliveryLocality).HasMaxLength(120).IsRequired();
        builder.Property(rfq => rfq.County).HasMaxLength(80).IsRequired();
        builder.Property(rfq => rfq.SupplierSearchRadiusKm).HasDefaultValue(30);
        builder.Property(rfq => rfq.AllowSplit).HasDefaultValue(false);
        builder.Property(rfq => rfq.DeliveryRequired).HasDefaultValue(false);
        builder.Property(rfq => rfq.ReopenCount).HasDefaultValue(0);
        builder.Property(rfq => rfq.FulfillmentStatus).HasDefaultValue(FulfillmentStatus.NotApplicable);
        builder.Property(rfq => rfq.Latitude).HasPrecision(9, 6);
        builder.Property(rfq => rfq.Longitude).HasPrecision(9, 6);
        builder.Property(rfq => rfq.EstimatedBudget).HasPrecision(18, 2);
        builder.Property(rfq => rfq.BuyerNotes).HasMaxLength(2000);
        builder.HasIndex(rfq => new { rfq.BuyerId, rfq.Status });
        builder.HasIndex(rfq => new { rfq.Status, rfq.QuotationDeadline });
        builder.HasOne<BuyerProfile>().WithMany().HasForeignKey(rfq => rfq.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(rfq => rfq.PrimaryCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(rfq => rfq.Items).WithOne().HasForeignKey(item => item.RfqId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(rfq => rfq.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
