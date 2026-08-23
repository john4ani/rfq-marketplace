using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapTenderConfiguration : IEntityTypeConfiguration<SeapTender>
{
    public void Configure(EntityTypeBuilder<SeapTender> builder)
    {
        builder.ToTable("SeapTenders");
        builder.HasKey(tender => tender.Id);
        builder.HasIndex(tender => tender.SeapExternalId).IsUnique();
        builder.Property(tender => tender.SeapExternalId).HasMaxLength(64).IsRequired();
        builder.Property(tender => tender.Title).HasMaxLength(500).IsRequired();
        builder.Property(tender => tender.Description).HasColumnType("text");
        builder.Property(tender => tender.CpvCodes).HasColumnType("text[]");
        builder.Property(tender => tender.Currency).HasMaxLength(8);
        builder.Property(tender => tender.BuyerName).HasMaxLength(300).IsRequired();
        builder.Property(tender => tender.EstimatedValue).HasPrecision(18, 2);
        builder.Property(tender => tender.Status).HasConversion<string>().HasMaxLength(24);
        builder.HasMany(tender => tender.Attachments).WithOne().HasForeignKey(attachment => attachment.TenderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(tender => tender.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
