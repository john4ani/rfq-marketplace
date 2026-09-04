using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapPreparedDocumentPageConfiguration : IEntityTypeConfiguration<SeapPreparedDocumentPage>
{
    public void Configure(EntityTypeBuilder<SeapPreparedDocumentPage> builder)
    {
        builder.ToTable("SeapPreparedDocumentPages");
        builder.HasKey(page => page.Id);
        // One ordered page per preparation — extraction is deterministic, so a repeated page insert is a bug.
        builder.HasIndex(page => new { page.PreparationId, page.PageNumber }).IsUnique();
        builder.Property(page => page.Text).HasColumnType("text").IsRequired();
        builder.Property(page => page.LetterCount).HasDefaultValue(0);
        builder.Property(page => page.Warning).HasMaxLength(255);
        builder.HasOne<SeapDocumentPreparation>().WithMany()
            .HasForeignKey(page => page.PreparationId).OnDelete(DeleteBehavior.Cascade);
    }
}
