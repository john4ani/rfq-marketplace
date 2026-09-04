using Marketplace.Domain.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SeapDocumentPreparationConfiguration : IEntityTypeConfiguration<SeapDocumentPreparation>
{
    public void Configure(EntityTypeBuilder<SeapDocumentPreparation> builder)
    {
        builder.ToTable("SeapDocumentPreparations");
        builder.HasKey(preparation => preparation.Id);
        builder.HasIndex(preparation => preparation.TenderId);
        builder.HasIndex(preparation => preparation.AttachmentId);
        // Versioned cache identity: the same file bytes prepared with the same extractor version exist at
        // most once — repeated DocumentPreparation runs return the cached row instead of re-preparing.
        builder.HasIndex(preparation => new
            {
                preparation.AttachmentId,
                preparation.SourceContentSha256,
                preparation.ExtractionVersion
            })
            .IsUnique();
        builder.Property(preparation => preparation.SourceContentSha256).HasMaxLength(64).IsRequired();
        builder.Property(preparation => preparation.ExtractionVersion).HasMaxLength(64).IsRequired();
        builder.Property(preparation => preparation.Status).HasConversion<string>().HasMaxLength(24)
            .HasDefaultValue(SeapDocumentPreparationStatus.Pending).IsRequired();
        builder.Property(preparation => preparation.AttemptCount).HasDefaultValue(0);
        builder.Property(preparation => preparation.WarningCount).HasDefaultValue(0);
        builder.Property(preparation => preparation.LastErrorCode).HasMaxLength(64);
        builder.Property(preparation => preparation.LastErrorMessage).HasColumnType("text");
        builder.HasOne<SeapTenderAttachment>().WithMany()
            .HasForeignKey(preparation => preparation.AttachmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SeapTender>().WithMany()
            .HasForeignKey(preparation => preparation.TenderId).OnDelete(DeleteBehavior.Cascade);
    }
}
