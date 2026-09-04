using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapPreparedDocumentSectionConfiguration : IEntityTypeConfiguration<SeapPreparedDocumentSection>
{
    public void Configure(EntityTypeBuilder<SeapPreparedDocumentSection> builder)
    {
        builder.ToTable("SeapPreparedDocumentSections");
        builder.HasKey(section => section.Id);
        // Ordered section list per preparation.
        builder.HasIndex(section => new { section.PreparationId, section.Ordinal }).IsUnique();
        builder.Property(section => section.RawCode).HasMaxLength(24).IsRequired();
        builder.Property(section => section.NormalizedRole).HasMaxLength(96).IsRequired();
        builder.Property(section => section.Title).HasMaxLength(255);
        builder.Property(section => section.Text).HasColumnType("text").IsRequired();
        builder.HasOne<SeapDocumentPreparation>().WithMany()
            .HasForeignKey(section => section.PreparationId).OnDelete(DeleteBehavior.Cascade);
    }
}
